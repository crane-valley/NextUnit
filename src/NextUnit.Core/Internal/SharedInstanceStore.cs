using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace NextUnit.Internal;

/// <summary>
/// The single store of shared data source instances behind <c>[ClassDataSource]</c> and
/// <c>[ValuesFrom]</c>.
/// </summary>
/// <remarks>
/// <para>
/// One store rather than one per expander: a data source type used through both attributes with the
/// same sharing scope is one instance, which is what "shared" says. Through 1.x each expander kept
/// its own four caches, so the same type reached by both attributes was instantiated twice.
/// </para>
/// <para>
/// The sharing scope is part of the key, so <see cref="SharedType.PerAssembly"/> and
/// <see cref="SharedType.PerSession"/> keep separate instances even though a single-assembly run
/// cannot tell the two lifetimes apart. They are documented as different scopes, and collapsing them
/// would change which tests share an instance rather than only which attribute they arrived through.
/// </para>
/// <para>
/// Everything the store hands out is released by <see cref="DisposeAllAsync"/> at the end of the test
/// session. Nothing called the 1.x equivalents, so a shared instance simply lived until the process
/// exited.
/// </para>
/// </remarks>
internal static class SharedInstanceStore
{
    // Null and "default" collapse onto one entry exactly as they did in 1.x. The generator requires a
    // key for SharedType.Keyed, so this only decides what a hand-built descriptor gets.
    private const string DefaultKey = "default";

    private static readonly ConcurrentDictionary<SharedInstanceKey, Lazy<SharedInstance>> _instances = new();
    private static long _sequence;

    /// <summary>
    /// Returns the instance the given sharing scope calls for, creating it on first use.
    /// </summary>
    /// <param name="sourceType">The data source type to instantiate.</param>
    /// <param name="sharedType">The sharing scope declared on the attribute.</param>
    /// <param name="key">The key for <see cref="SharedType.Keyed"/>; ignored for other scopes.</param>
    /// <param name="testClass">The test class the data source was declared on.</param>
    /// <param name="factory">
    /// The generated factory for the type, preferred over reflection when the generator emitted one.
    /// </param>
    public static object GetOrCreate(
        Type sourceType,
        SharedType sharedType,
        string? key,
        Type testClass,
        DataSourceProviderDelegate? factory)
    {
        // An unrecognized scope is treated as None rather than sharing under a key nothing else can
        // reproduce: a new enum member must opt into sharing deliberately.
        if (!TryCreateKey(sourceType, sharedType, key, testClass, out var instanceKey))
        {
            return CreateInstance(sourceType, factory);
        }

        // Lazy rather than a bare GetOrAdd factory: ConcurrentDictionary may run a losing factory and
        // throw its result away, and an instance the store never records is an instance nothing ever
        // disposes.
        var lazy = _instances.GetOrAdd(
            instanceKey,
            _ => new Lazy<SharedInstance>(
                () => new SharedInstance(CreateInstance(sourceType, factory), Interlocked.Increment(ref _sequence)),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return lazy.Value.Instance;
        }
        catch
        {
            // A Lazy remembers the exception its factory threw. Evicting the failed entry keeps the
            // 1.x behavior, where a failed creation was never cached and the next expansion retried.
            _instances.TryRemove(new KeyValuePair<SharedInstanceKey, Lazy<SharedInstance>>(instanceKey, lazy));
            throw;
        }
    }

    /// <summary>
    /// Disposes every shared instance and empties the store, in reverse creation order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reverse creation order is the same order a nest of <c>using</c> blocks would unwind in, so a
    /// data source built on top of an earlier one is released first.
    /// </para>
    /// <para>
    /// Every instance is disposed even after one of them throws, and the failures are reported
    /// together: the caller is session teardown, which has no other way to learn that a data source
    /// failed to release its resources.
    /// </para>
    /// <para>
    /// An entry whose instance is still being created is dropped rather than awaited. The store is
    /// emptied at the end of a session, when no expansion is left to be in flight, and blocking here
    /// on a data source constructor that never returns would hang the whole run instead.
    /// </para>
    /// </remarks>
    /// <exception cref="AggregateException">More than one instance failed to dispose.</exception>
    public static async ValueTask DisposeAllAsync()
    {
        var removed = new List<SharedInstance>();

        foreach (var key in _instances.Keys)
        {
            if (_instances.TryRemove(key, out var lazy) && lazy.IsValueCreated)
            {
                removed.Add(lazy.Value);
            }
        }

        removed.Sort(static (left, right) => right.Sequence.CompareTo(left.Sequence));

        List<Exception>? failures = null;

        foreach (var entry in removed)
        {
            try
            {
                await DisposeHelper.PreferAsyncDisposeAsync(entry.Instance).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ExceptionHelper.IsCriticalException(ex))
            {
                (failures ??= []).Add(ex);
            }
        }

        if (failures is null)
        {
            return;
        }

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        throw new AggregateException("One or more shared data source instances failed to dispose.", failures);
    }

    /// <summary>
    /// Disposes every shared instance from a caller that has no asynchronous context, blocking on
    /// asynchronous disposal.
    /// </summary>
    /// <remarks>
    /// This exists for the VSTest adapter, whose <c>ITestExecutor</c> contract is synchronous and
    /// already blocks on the run itself.
    /// </remarks>
    public static void DisposeAll() =>
        DisposeAllAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();

    /// <summary>
    /// Builds the key an instance is shared under, or reports that the scope shares nothing.
    /// </summary>
    private static bool TryCreateKey(
        Type sourceType,
        SharedType sharedType,
        string? key,
        Type testClass,
        out SharedInstanceKey instanceKey)
    {
        switch (sharedType)
        {
            case SharedType.Keyed:
                instanceKey = new SharedInstanceKey(sharedType, sourceType, null, key ?? DefaultKey);
                return true;

            case SharedType.PerClass:
                instanceKey = new SharedInstanceKey(sharedType, sourceType, testClass, null);
                return true;

            case SharedType.PerAssembly:
            case SharedType.PerSession:
                instanceKey = new SharedInstanceKey(sharedType, sourceType, null, null);
                return true;

            default:
                instanceKey = default;
                return false;
        }
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2067",
        Justification = "The source generator roots class data source constructors with DynamicDependency.")]
    private static object CreateInstance(
        Type sourceType,
        DataSourceProviderDelegate? factory)
    {
        try
        {
            if (factory is not null)
            {
                return factory()
                    ?? throw new InvalidOperationException(
                        $"Failed to create instance of '{sourceType.FullName}': factory returned null");
            }

            return Activator.CreateInstance(sourceType)
                ?? throw new InvalidOperationException(
                    $"Failed to create instance of '{sourceType.FullName}': Activator returned null");
        }
        catch (TargetInvocationException ex)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of '{sourceType.FullName}'",
                ex.InnerException ?? ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of '{sourceType.FullName}'",
                ex);
        }
    }

    /// <summary>
    /// Identifies one shared instance: its scope, its type, and whatever else that scope shares by.
    /// </summary>
    /// <remarks>
    /// The source type is the <see cref="Type"/> itself rather than its name, so two identically
    /// named types from different assemblies cannot collide on one instance.
    /// </remarks>
    private readonly record struct SharedInstanceKey(
        SharedType Scope,
        Type SourceType,
        Type? TestClass,
        string? Key);

    /// <summary>
    /// One stored instance and the position it was created at, which fixes the disposal order.
    /// </summary>
    private sealed class SharedInstance
    {
        public SharedInstance(object instance, long sequence)
        {
            Instance = instance;
            Sequence = sequence;
        }

        public object Instance { get; }

        public long Sequence { get; }
    }
}
