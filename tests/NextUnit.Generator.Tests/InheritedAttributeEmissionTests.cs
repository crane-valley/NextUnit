using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins which attributes a test inherits from a base class or an overridden method, and which it
/// does not.
/// </summary>
/// <remarks>
/// The rule under test: an attribute that configures how a test runs or how it is labelled is
/// inherited, and an attribute that decides what the test set is -- whether a method is a test, what
/// data it runs with, how many cases it expands to, what it depends on -- is not. The negative cases
/// carry as much weight as the positive ones, because the way this rule fails in practice is by
/// quietly widening.
/// </remarks>
public class InheritedAttributeEmissionTests
{
    [Fact]
    public async Task ConfigurationAttributesOnABaseClass_ReachTheDerivedTestAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            [Timeout(1234)]
            [Retry(3)]
            [Category("Integration")]
            [Tag("slow")]
            [ExecutionPriority(7)]
            [ParallelGroup("db")]
            [NotInParallel]
            [Flaky("shared database")]
            [Culture("ja-JP")]
            public class BaseTests
            {
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                public void Run() { }
            }
            """);

        Assert.Contains("TimeoutMs = 1234", registry);
        Assert.Contains("Count = 3", registry);
        Assert.Contains("\"Integration\"", registry);
        Assert.Contains("\"slow\"", registry);
        Assert.Contains("Priority = 7", registry);
        Assert.Contains("ParallelGroup = \"db\"", registry);
        Assert.Contains("NotInParallel = true", registry);
        Assert.Contains("IsFlaky = true", registry);
        Assert.Contains("FlakyReason = \"shared database\"", registry);
        Assert.Contains("CultureName = \"ja-JP\"", registry);
    }

    [Fact]
    public async Task NearestDeclarationWins_AcrossTheWholeBaseChainAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            [Timeout(1000)]
            public class RootTests
            {
            }

            [Timeout(2000)]
            public class MiddleTests : RootTests
            {
            }

            public class LeafTests : MiddleTests
            {
                [Test]
                public void Run() { }
            }
            """);

        Assert.Contains("TimeoutMs = 2000", registry);
        Assert.False(registry.Contains("TimeoutMs = 1000", StringComparison.Ordinal), "the farther declaration must not win");
    }

    [Fact]
    public async Task OverriddenMethodDeclaration_IsInheritedAndLosesToTheOverrideAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Test]
                [Timeout(1000)]
                [Category("FromBase")]
                public virtual void Run() { }
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                [Timeout(2000)]
                public override void Run() { }
            }
            """);

        // The override chain is a level like any other: the nearer declaration replaces the timeout,
        // and the category accumulates because [Category] allows multiple.
        Assert.Contains("TimeoutMs = 2000", registry);
        Assert.Contains("\"FromBase\"", registry);
    }

    [Fact]
    public async Task CategoriesAccumulateAcrossEveryLevel_AndKeepDuplicatesAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            [Category("Shared")]
            public class BaseTests
            {
            }

            [Category("Shared")]
            [Category("Derived")]
            public class DerivedTests : BaseTests
            {
                [Test]
                [Category("Method")]
                public void Run() { }
            }
            """);

        // Method levels come before type levels, each nearest first. Duplicates are kept, because
        // collapsing them is a separate change to what ITestContext.Categories reports.
        Assert.Contains("Categories = new string[] { \"Method\", \"Shared\", \"Derived\", \"Shared\" }", registry);
    }

    [Fact]
    public async Task TestSetAttributes_AreNotInheritedAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Test]
                [Skip("base only")]
                [DisplayName("Base name")]
                public virtual void Run() { }
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                public override void Run() { }
            }
            """);

        // Two test cases are emitted, and only the base one carries the skip and the display name:
        // an override that re-declares [Test] is an explicit new registration, so its skip and its
        // name have to be explicit too.
        Assert.Equal(1, Occurrences(registry, "IsSkipped = true"));
        Assert.Equal(1, Occurrences(registry, "IsSkipped = false"));
        Assert.Equal(1, Occurrences(registry, "CustomDisplayNameTemplate = \"Base name\""));
    }

    [Fact]
    public async Task TestAttributeItself_IsNotInheritedAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Test]
                public virtual void Run() { }
            }

            public class DerivedTests : BaseTests
            {
                public override void Run() { }
            }
            """);

        // Discovery is unchanged by this feature: the test belongs to the class that declares [Test].
        Assert.Contains("BaseTests.Run", registry);
        Assert.False(registry.Contains("DerivedTests.Run", StringComparison.Ordinal), "an override without [Test] must not be discovered");
    }

    [Fact]
    public async Task DependsOnIsNotInherited_BecauseItsIdsAreTypeQualifiedAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Test]
                public void First() { }

                [Test]
                [DependsOn(nameof(First))]
                public virtual void Second() { }
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                public override void Second() { }
            }
            """);

        // An inherited [DependsOn("First")] would resolve against the derived type and silently
        // retarget, so the attribute stays with the declaration that wrote it.
        Assert.Equal(1, Occurrences(registry, "DependsOnId = "));
    }

    [Fact]
    public async Task InheritedRetryPolicyTheRegistryCannotName_ReportsNextUnit015Async()
    {
        var fixtureLibrary = await CompileLibraryAsync("Fixtures", """
            using NextUnit;

            namespace Fixtures;

            internal sealed class HiddenPolicy : IRetryPolicy
            {
                public System.Threading.Tasks.ValueTask<bool> ShouldRetryAsync(RetryContext context) =>
                    new System.Threading.Tasks.ValueTask<bool>(true);
            }

            [Retry<HiddenPolicy>(2)]
            public class SharedFixture
            {
            }
            """);

        var (registry, diagnostics) = await GenerateWithDiagnosticsAsync("""
            using Fixtures;
            using NextUnit;

            namespace TestProject;

            public class DerivedTests : SharedFixture
            {
                [Test]
                public void Run() { }
            }
            """,
            fixtureLibrary);

        // NU0016 sees a directly applied policy in the compilation that wrote it; only an inherited
        // one can be reachable there and unreachable here.
        Assert.True(diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT015"), FormatIds(diagnostics));
        Assert.False(registry.Contains("HiddenPolicy", StringComparison.Ordinal), "the unreachable policy must not be emitted");
    }

    [Fact]
    public async Task DirectlyAppliedFormatterTheRegistryCannotName_ReportsNextUnit015Async()
    {
        var (registry, diagnostics) = await GenerateWithDiagnosticsAsync("""
            using NextUnit;

            namespace TestProject;

            public class OwnTests
            {
                private sealed class HiddenFormatter : IDisplayNameFormatter
                {
                    public string Format(DisplayNameContext context) => context.MethodName;
                }

                [Test]
                [DisplayNameFormatter(typeof(HiddenFormatter))]
                public void Run() { }
            }
            """);

        // No analyzer covers formatter accessibility, so the non-generic form is checked wherever it
        // is declared rather than only when inherited.
        Assert.True(diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT015"), FormatIds(diagnostics));
        Assert.False(registry.Contains("HiddenFormatter", StringComparison.Ordinal), "the unreachable formatter must not be emitted");
    }

    [Fact]
    public async Task GenericFormatterTheRegistryCannotName_ReportsNextUnit015Async()
    {
        var (_, diagnostics) = await GenerateWithDiagnosticsAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                protected sealed class HiddenFormatter : IDisplayNameFormatter
                {
                    public string Format(DisplayNameContext context) => context.MethodName;
                }
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                [DisplayNameFormatter<HiddenFormatter>]
                public void Run() { }
            }
            """);

        Assert.True(diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT015"), FormatIds(diagnostics));
    }

    [Fact]
    public async Task ReachableInheritedFormatter_IsEmittedAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public sealed class UpperCaseFormatter : IDisplayNameFormatter
            {
                public string Format(DisplayNameContext context) => context.MethodName;
            }

            [DisplayNameFormatter<UpperCaseFormatter>]
            public class BaseTests
            {
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                public void Run() { }
            }
            """);

        Assert.Contains("DisplayNameFormatterType = typeof(global::TestProject.UpperCaseFormatter)", registry);
    }

    [Fact]
    public async Task DirectlyAppliedRetryPolicyTheRegistryCannotName_IsStillEmittedAsync()
    {
        var (registry, diagnostics) = await GenerateWithDiagnosticsAsync("""
            using NextUnit;

            namespace TestProject;

            public class OwnTests
            {
                private sealed class HiddenPolicy : IRetryPolicy
                {
                    public System.Threading.Tasks.ValueTask<bool> ShouldRetryAsync(RetryContext context) =>
                        new System.Threading.Tasks.ValueTask<bool>(true);
                }

                [Test]
                [Retry<HiddenPolicy>(2)]
                public void Run() { }
            }
            """);

        // NU0016 owns this case and can be suppressed. Dropping the policy here would turn a
        // suppressed report into a silent switch to the default retry behavior, so the type is
        // emitted and the build fails on the CS0122 NU0016 warned about.
        Assert.Contains("HiddenPolicy", registry);
        Assert.False(
            diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT015"),
            FormatIds(diagnostics));
    }

    private static string FormatIds(IReadOnlyList<Diagnostic> diagnostics) =>
        $"reported: {string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Id))}";

    private static int Occurrences(string registry, string value) =>
        registry.Split([value], StringSplitOptions.None).Length - 1;

    private static async Task<string> GenerateAsync(string source, params MetadataReference[] references) =>
        (await GenerateWithDiagnosticsAsync(source, references)).Registry;

    private static async Task<(string Registry, IReadOnlyList<Diagnostic> Diagnostics)> GenerateWithDiagnosticsAsync(
        string source,
        params MetadataReference[] references)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken,
            references);

        var result = GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGenerators(compilation, cancellationToken)
            .GetRunResult();

        var registry = result.Results.Single().GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();

        return (registry, result.Diagnostics);
    }

    private static async Task<MetadataReference> CompileLibraryAsync(string assemblyName, string source)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken,
            additionalReferences: null,
            assemblyName: assemblyName);

        Assert.Empty(compilation
            .GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToList());

        return compilation.ToMetadataReference();
    }
}
