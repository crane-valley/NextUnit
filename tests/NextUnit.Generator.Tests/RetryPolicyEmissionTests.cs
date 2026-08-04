using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins how <c>[Retry&lt;TPolicy&gt;]</c> reaches the generated registry.
/// </summary>
/// <remarks>
/// Asserted line by line rather than with a full snapshot baseline because the emission is one
/// conditional property. The existing snapshots already cover the rest of the descriptor, and their
/// staying byte-identical is itself the proof that a test without a policy emits nothing new.
/// </remarks>
public class RetryPolicyEmissionTests
{
    private const string PolicySource = """
        using System.Threading.Tasks;
        using NextUnit;

        namespace TestProject;

        public sealed class RetryOnTimeout : IRetryPolicy
        {
            public ValueTask<bool> ShouldRetryAsync(RetryContext context) =>
                ValueTask.FromResult(context.Exception is System.TimeoutException);
        }
        """;

    [Fact]
    public async Task PolicyRetry_EmitsADirectConstructorCallAsync()
    {
        var registry = await GenerateRegistryAsync(PolicySource + """


            namespace TestProject;

            public class RetryTests
            {
                [Test]
                [Retry<RetryOnTimeout>(3, 25)]
                public void Flaky()
                {
                }
            }
            """);

        // A direct `new` keeps the policy off every reflection path and lets the trimmer see it, so
        // the emitted text - not just the behavior - is what this pins.
        Assert.Contains(
            "PolicyFactory = static () => new global::TestProject.RetryOnTimeout(),",
            registry);
        Assert.Contains("Count = 3,", registry);
        Assert.Contains("DelayMs = 25,", registry);
    }

    [Fact]
    public async Task PlainRetry_EmitsNoPolicyFactoryAsync()
    {
        var registry = await GenerateRegistryAsync("""
            using NextUnit;

            namespace TestProject;

            public class RetryTests
            {
                [Test]
                [Retry(3)]
                public void Flaky()
                {
                }
            }
            """);

        // The compatibility default costs nothing in the generated file.
        Assert.False(
            registry.Contains("PolicyFactory", StringComparison.Ordinal),
            "A retry without a policy must not emit a policy factory.");
    }

    [Fact]
    public async Task ClassLevelPolicyRetry_AppliesToEveryTestInTheClassAsync()
    {
        var registry = await GenerateRegistryAsync(PolicySource + """


            namespace TestProject;

            [Retry<RetryOnTimeout>(2)]
            public class RetryTests
            {
                [Test]
                public void First()
                {
                }

                [Test]
                public void Second()
                {
                }
            }
            """);

        var occurrences = registry.Split(
            ["PolicyFactory = static () => new global::TestProject.RetryOnTimeout(),"],
            StringSplitOptions.None).Length - 1;

        Assert.Equal(2, occurrences);
    }

    /// <summary>
    /// A method that restates the budget takes the whole declaration, policy included - which here
    /// means no policy at all.
    /// </summary>
    [Fact]
    public async Task MethodRetry_OverridesTheClassPolicyEntirelyAsync()
    {
        var registry = await GenerateRegistryAsync(PolicySource + """


            namespace TestProject;

            [Retry<RetryOnTimeout>(2)]
            public class RetryTests
            {
                [Test]
                [Retry(5)]
                public void Overriding()
                {
                }
            }
            """);

        Assert.Contains("Count = 5,", registry);
        Assert.False(
            registry.Contains("PolicyFactory", StringComparison.Ordinal),
            "The method-level retry must replace the class policy, not inherit it.");
    }

    private static async Task<string> GenerateRegistryAsync(string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken);
        var driver = GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGenerators(compilation, cancellationToken);

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();
    }
}
