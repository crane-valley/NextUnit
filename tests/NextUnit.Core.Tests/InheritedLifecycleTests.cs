using System.Collections.Concurrent;

namespace NextUnit.Core.Tests;

/// <summary>
/// Runs the inherited lifecycle and attribute rules end to end, through the generator and the
/// execution engine, rather than against the emitted text.
/// </summary>
public abstract class InheritedLifecycleBase
{
    /// <summary>
    /// _recorded on the test instance, so two tests running in parallel cannot see each other.
    /// </summary>
    protected List<string> Steps { get; } = new();

    /// <summary>
    /// Set by the base hook and cleared by an override that opts out, which is the escape hatch a
    /// suite reaches for when an inherited setup must not run for one derived class.
    /// </summary>
    protected bool BaseSetupRan { get; private set; }

    protected int SetupCount { get; private set; }

    [Before(LifecycleScope.Test)]
    public void BaseBefore()
    {
        BaseSetupRan = true;
        Steps.Add("base-before");
    }

    [Before(LifecycleScope.Test)]
    public virtual void CountableSetup() => SetupCount++;

    [After(LifecycleScope.Test)]
    public void BaseAfter() => InheritedLifecycleOrderRecorder.Record("base-after");
}

public class InheritedLifecycleTests : InheritedLifecycleBase
{
    [Before(LifecycleScope.Test)]
    public void DerivedBefore() => Steps.Add("derived-before");

    [Test]
    public void BaseBeforeHook_RunsForADerivedClass_AndRunsFirst()
    {
        Assert.True(BaseSetupRan);
        Assert.Equal("base-before,derived-before", string.Join(",", Steps));
    }

    [Test]
    public void OverriddenHook_RunsOnceAtTheMostDerivedOverride()
    {
        // The base declaration is the one the registry emits, and it dispatches virtually, so an
        // override neither doubles the hook nor loses it.
        Assert.Equal(1, SetupCount);
    }
}

/// <summary>
/// Opts out of one inherited hook by overriding it with a body that does nothing.
/// </summary>
public class InheritedLifecycleOptOutTests : InheritedLifecycleBase
{
    public override void CountableSetup()
    {
        // Deliberately empty: this is the documented opt-out.
    }

    [Test]
    public void OverridingAnInheritedHook_SuppressesTheBaseBody()
    {
        Assert.Equal(0, SetupCount);

        // Only the overridden hook is opted out of; the rest of the base setup still runs.
        Assert.True(BaseSetupRan);
    }
}

/// <summary>
/// Records hook order across tests, which no single test body can observe: a test cannot see its own
/// <c>[After]</c> hooks run.
/// </summary>
public static class InheritedLifecycleOrderRecorder
{
    private static readonly ConcurrentQueue<string> _recorded = new();

    public static void Record(string step) => _recorded.Enqueue(step);

    public static IReadOnlyList<string> Steps => _recorded.ToArray();
}

public class InheritedTeardownOrderBase
{
    [After(LifecycleScope.Test)]
    public void BaseAfter() => InheritedLifecycleOrderRecorder.Record("base-after");
}

[NotInParallel]
public class InheritedTeardownOrderTests : InheritedTeardownOrderBase
{
    [After(LifecycleScope.Test)]
    public void DerivedAfter() => InheritedLifecycleOrderRecorder.Record("derived-after");

    [Test]
    public void TriggersTheTeardownHooks()
    {
    }

    [Test]
    [DependsOn(nameof(TriggersTheTeardownHooks))]
    public void TeardownUnwindsFromDerivedToBase()
    {
        var steps = InheritedLifecycleOrderRecorder.Steps;
        var derived = steps.ToList().IndexOf("derived-after");

        Assert.True(derived >= 0, "the derived teardown did not run");
        Assert.Equal("base-after", steps[derived + 1]);
    }
}

[Category("InheritedCategory")]
[Timeout(45_000)]
[Retry(2)]
public class InheritedAttributeBase
{
}

public class InheritedAttributeTests : InheritedAttributeBase
{
    private static int _attempts;

    private readonly ITestContext _context;

    public InheritedAttributeTests(ITestContext context) => _context = context;

    [Test]
    public void CategoryDeclaredOnTheBaseClass_ReachesTheTest() =>
        Assert.Contains("InheritedCategory", _context.Categories);

    [Test]
    public void TimeoutDeclaredOnTheBaseClass_ReachesTheTest() =>
        Assert.Equal(45_000, _context.TimeoutMs);

    [Test]
    public void RetryDeclaredOnTheBaseClass_GivesTheTestASecondAttempt()
    {
        _attempts++;

        // The first attempt fails and the inherited budget grants the second, which passes. Without
        // inheritance this test would fail on its only attempt.
        Assert.True(_attempts > 1, "the inherited retry budget did not grant a second attempt");
    }
}
