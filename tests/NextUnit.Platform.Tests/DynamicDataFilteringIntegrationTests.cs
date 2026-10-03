using System.Reflection;
using Microsoft.Testing.Platform.TestHost;
using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

[NotInParallel(FilterEnvironmentConstraint.Key)]
public sealed class DynamicDataFilteringIntegrationTests
{
    [Test]
    public Task IncludeCategory_EnumeratesUnselectedProviderAsync() =>
        VerifyAsync("Safe", null, null, false, false, 1, 0);

    [Test]
    public Task ExcludeCategory_DoesNotEnumerateProviderAsync() =>
        VerifyAsync(null, "Dangerous", null, false, false, 0, 0);

    [Test]
    public Task RowOnlyCategoryInclude_SelectsRowAsync() =>
        VerifyAsync("Safe", null, null, true, false, 1, 1);

    [Test]
    public Task NameIncludeOrCategoryInclude_SelectsRowAsync() =>
        VerifyAsync("Safe", null, "Dangerous*", false, false, 1, 1);

    [Test]
    public Task DeferredUnselectedSource_DoesNotEnumerateProviderAsync() =>
        VerifyAsync("Safe", null, null, false, true, 0, 0);

    private static async Task VerifyAsync(
        string? includeCategory,
        string? excludeCategory,
        string? namePattern,
        bool rowHasIncludedCategory,
        bool deferred,
        int expectedProviderCalls,
        int expectedBodyCalls)
    {
        using var include = EnvironmentVariableGuard.Set("NEXTUNIT_INCLUDE_CATEGORIES", includeCategory);
        using var exclude = EnvironmentVariableGuard.Set("NEXTUNIT_EXCLUDE_CATEGORIES", excludeCategory);
        using var includeTags = EnvironmentVariableGuard.Set("NEXTUNIT_INCLUDE_TAGS", null);
        using var excludeTags = EnvironmentVariableGuard.Set("NEXTUNIT_EXCLUDE_TAGS", null);
        using var names = EnvironmentVariableGuard.Set("NEXTUNIT_TEST_NAME", namePattern);
        using var regex = EnvironmentVariableGuard.Set("NEXTUNIT_TEST_NAME_REGEX", null);
        using var explicitTests = EnvironmentVariableGuard.Set("NEXTUNIT_INCLUDE_EXPLICIT", null);

        var originalRegistry = GeneratedTestRegistryStore.Current;
        Assert.NotNull(originalRegistry);
        var providerCalls = 0;
        var bodyCalls = 0;
        var descriptor = new TestDataDescriptor
        {
            BaseId = "SecurityProbe.DangerousRows",
            DisplayName = "DangerousRows",
            TestClass = typeof(ProbeTarget),
            MethodName = nameof(ProbeTarget.DangerousRows),
            DataSourceName = "CounterOnlyRows",
            ParameterTypes = [typeof(int)],
            Categories = ["Dangerous"],
            DeferredEnumeration = deferred,
            TestClassFactory = static (_, _) => new ProbeTarget(),
            TestMethodWithArguments = (_, _, _) =>
            {
                Interlocked.Increment(ref bodyCalls);
                return Task.CompletedTask;
            },
            DataSourceProvider = () =>
            {
                Interlocked.Increment(ref providerCalls);
                return new[]
                {
                    new TestDataRow<int>(1, categories: rowHasIncludedCategory ? ["Safe"] : [])
                };
            }
        };

        try
        {
            GeneratedTestRegistryStore.Register(new ProbeRegistry(descriptor));
            using var framework = new NextUnitFramework(null!, new NullServiceProvider());
            var messageBus = new RecordingMessageBus();
            await framework.DiscoverAsync(new SessionUid("security-high-probe"), messageBus, CancellationToken.None);

            Assert.Equal(expectedProviderCalls, providerCalls);
            Assert.Equal(0, bodyCalls);
            Assert.Equal(expectedBodyCalls, messageBus.TestNodeUpdates.Count);

            // Read discovery's cached selection rather than duplicating its filtering in the probe.
            var getTestCases = typeof(NextUnitFramework).GetMethod(
                "GetTestCasesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var selected = await (Task<IReadOnlyList<TestCaseDescriptor>>)getTestCases.Invoke(
                framework, [CancellationToken.None])!;
            var sink = new RecordingSink();
            await new TestExecutionEngine().RunAsync(selected, sink, CancellationToken.None);

            Assert.Equal(expectedProviderCalls, providerCalls);
            Assert.Equal(expectedBodyCalls, bodyCalls);
            Assert.Equal(expectedBodyCalls, sink.Passed.Count);
            Assert.Empty(sink.Errors);
            Assert.Empty(sink.Failed);
            Console.WriteLine($"Security probe: provider calls={providerCalls}, test body calls={bodyCalls}, discovered rows={messageBus.TestNodeUpdates.Count}");
        }
        finally
        {
            GeneratedTestRegistryStore.Register(originalRegistry!);
        }
    }

    private sealed class ProbeTarget
    {
        public void DangerousRows(int value)
        {
        }
    }

    private sealed class ProbeRegistry(TestDataDescriptor descriptor) : IGeneratedTestRegistry
    {
        public IReadOnlyList<TestCaseDescriptor> TestCases => [];
        public IReadOnlyList<TestDataDescriptor> TestDataDescriptors => [descriptor];
        public IReadOnlyList<ClassDataSourceDescriptor> ClassDataSourceDescriptors => [];
        public IReadOnlyList<CombinedDataSourceDescriptor> CombinedDataSourceDescriptors => [];
        public LifecycleMethodDelegate[] GlobalBeforeAssemblyMethods => [];
        public LifecycleMethodDelegate[] GlobalAfterAssemblyMethods => [];
        public LifecycleMethodDelegate[] GlobalBeforeSessionMethods => [];
        public LifecycleMethodDelegate[] GlobalAfterSessionMethods => [];
    }
}
