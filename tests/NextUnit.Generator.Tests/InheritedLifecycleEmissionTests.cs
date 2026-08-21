using Microsoft.CodeAnalysis;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Pins how lifecycle hooks declared on a base test class reach the generated registry.
/// </summary>
/// <remarks>
/// Asserted on the emitted delegate text rather than through a snapshot baseline, because what these
/// tests are about is the order of the delegates and the type each one casts to, and a baseline
/// would tie every case to the whole surrounding descriptor.
/// </remarks>
public class InheritedLifecycleEmissionTests
{
    [Fact]
    public async Task BaseHooks_RunBeforeDerivedHooks_AndTearDownInReverseAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Test)]
                public void BaseBefore() { }

                [After(LifecycleScope.Test)]
                public void BaseAfter() { }
            }

            public class DerivedTests : BaseTests
            {
                [Before(LifecycleScope.Test)]
                public void DerivedBefore() { }

                [After(LifecycleScope.Test)]
                public void DerivedAfter() { }

                [Test]
                public void Run() { }
            }
            """);

        AssertOrder(registry, "BeforeTestMethods", "BaseBefore", "DerivedBefore");
        AssertOrder(registry, "AfterTestMethods", "DerivedAfter", "BaseAfter");
    }

    [Fact]
    public async Task SeveralHooksInOneClass_KeepDeclarationOrderInBothDirectionsAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [After(LifecycleScope.Test)]
                public void BaseFirst() { }

                [After(LifecycleScope.Test)]
                public void BaseSecond() { }
            }

            public class DerivedTests : BaseTests
            {
                [After(LifecycleScope.Test)]
                public void DerivedFirst() { }

                [After(LifecycleScope.Test)]
                public void DerivedSecond() { }

                [Test]
                public void Run() { }
            }
            """);

        // Only the class levels reverse. Reversing the flat list would put DerivedSecond first.
        AssertOrder(registry, "AfterTestMethods", "DerivedFirst", "DerivedSecond", "BaseFirst", "BaseSecond");
    }

    [Fact]
    public async Task OverriddenHook_IsEmittedOnceFromTheBaseSlotAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Test)]
                public virtual void Setup() { }
            }

            public class DerivedTests : BaseTests
            {
                [Before(LifecycleScope.Test)]
                public override void Setup() { }

                [Before(LifecycleScope.Test)]
                public void ExtraSetup() { }

                [Test]
                public void Run() { }
            }
            """);

        // Re-declaring [Before] on the override must not add a second call, and must not move the
        // hook past ExtraSetup: the cast is to the base type and dispatches to the override.
        Assert.Equal(1, Occurrences(registry, ".Setup()"));
        AssertOrder(registry, "BeforeTestMethods", "Setup", "ExtraSetup");
        Assert.Contains("((global::TestProject.BaseTests)instance).Setup()", registry);
    }

    [Fact]
    public async Task UnannotatedOverride_RunsTheDerivedBodyFromTheBaseDeclarationAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Test)]
                public virtual void Setup() { }
            }

            public class DerivedTests : BaseTests
            {
                public override void Setup() { }

                [Test]
                public void Run() { }
            }
            """);

        Assert.Equal(1, Occurrences(registry, ".Setup()"));
        Assert.Contains("((global::TestProject.BaseTests)instance).Setup()", registry);
    }

    [Fact]
    public async Task HiddenHook_IsASecondHookRatherThanAReplacementAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Test)]
                public void Setup() { }
            }

            public class DerivedTests : BaseTests
            {
                [Before(LifecycleScope.Test)]
                public new void Setup() { }

                [Test]
                public void Run() { }
            }
            """);

        // A `new` method is a different method, so both hooks run, each through its declaring type.
        Assert.Contains("((global::TestProject.BaseTests)instance).Setup()", registry);
        Assert.Contains("((global::TestProject.DerivedTests)instance).Setup()", registry);
    }

    [Fact]
    public async Task UnannotatedHidingMethod_DoesNotCaptureTheBaseHookCallAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Test)]
                public void Setup() { }
            }

            public class DerivedTests : BaseTests
            {
                public new void Setup() { }

                [Test]
                public void Run() { }
            }
            """);

        // Casting to the test class would bind the call to the unannotated derived method, so the
        // hook the user actually wrote would never run.
        Assert.Contains("((global::TestProject.BaseTests)instance).Setup()", registry);
        Assert.False(registry.Contains("((global::TestProject.DerivedTests)instance).Setup()", StringComparison.Ordinal), "((global::TestProject.DerivedTests)instance).Setup()" + " must not be emitted");
    }

    [Fact]
    public async Task HookOverloads_AreBothEmittedAsync()
    {
        var registry = await GenerateAsync("""
            using System.Threading;
            using System.Threading.Tasks;
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Test)]
                public void Setup() { }
            }

            public class DerivedTests : BaseTests
            {
                [Before(LifecycleScope.Test)]
                public Task Setup(CancellationToken token) => Task.CompletedTask;

                [Test]
                public void Run() { }
            }
            """);

        Assert.Contains("((global::TestProject.BaseTests)instance).Setup()", registry);
        Assert.Contains("((global::TestProject.DerivedTests)instance).Setup(ct)", registry);
    }

    [Fact]
    public async Task OverrideDeclaringADifferentScope_KeepsBothScopesAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Test)]
                public virtual void Setup() { }
            }

            public class DerivedTests : BaseTests
            {
                [Before(LifecycleScope.Class)]
                public override void Setup() { }

                [Test]
                public void Run() { }
            }
            """);

        // Selection runs per scope, so collapsing the override chain cannot lose the scope only one
        // of the two declarations names.
        AssertOrder(registry, "BeforeTestMethods", "Setup");
        AssertOrder(registry, "BeforeClassMethods", "Setup");
    }

    [Fact]
    public async Task GenericBaseClass_IsCastToItsConstructedFormAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests<T>
            {
                [Before(LifecycleScope.Test)]
                public void Setup() { }
            }

            public class DerivedTests : BaseTests<int>
            {
                [Test]
                public void Run() { }
            }
            """);

        // `((BaseTests<T>)instance)` is not valid C#, so the open definition cannot be the cast.
        Assert.Contains("((global::TestProject.BaseTests<int>)instance).Setup()", registry);
    }

    [Fact]
    public async Task StaticBaseHook_IsCalledThroughItsDeclaringTypeAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Class)]
                public static void SetupClass() { }
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                public void Run() { }
            }
            """);

        Assert.Contains("global::TestProject.BaseTests.SetupClass()", registry);
    }

    [Fact]
    public async Task AssemblyScopedBaseHook_IsNotRepeatedPerDerivedClassAsync()
    {
        var registry = await GenerateAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Assembly)]
                public static void StartOnce() { }
            }

            public class FirstTests : BaseTests
            {
                [Test]
                public void Run() { }
            }

            public class SecondTests : BaseTests
            {
                [Test]
                public void Run() { }
            }
            """);

        // Assembly and Session hooks run once for the whole run, so inheriting them would mean
        // running them once per derived class instead, which is a different thing.
        Assert.Equal(1, Occurrences(registry, ".StartOnce()"));
    }

    [Fact]
    public async Task BaseClassInAReferencedAssembly_ContributesItsHooksAsync()
    {
        var fixtureLibrary = await CompileLibraryAsync("Fixtures", """
            using NextUnit;

            namespace Fixtures;

            public class SharedFixture
            {
                [Before(LifecycleScope.Test)]
                public void SharedSetup() { }
            }
            """);

        var registry = await GenerateAsync("""
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

        // The syntax providers see only this compilation, so a shared fixture package is exactly the
        // case that stays silently dead if the hooks are not walked from the test transform.
        Assert.Contains("((global::Fixtures.SharedFixture)instance).SharedSetup()", registry);
    }

    [Fact]
    public async Task InaccessibleBaseHook_ReportsNextUnit014AndIsNotEmittedAsync()
    {
        var (registry, diagnostics) = await GenerateWithDiagnosticsAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Test)]
                protected void ProtectedSetup() { }
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                public void Run() { }
            }
            """);

        Assert.True(diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT014"), FormatIds(diagnostics));
        Assert.False(registry.Contains("ProtectedSetup", StringComparison.Ordinal), "ProtectedSetup" + " must not be emitted");
    }

    [Fact]
    public async Task InaccessibleHookOnTheTestClassItself_ReportsNextUnit014Async()
    {
        var (registry, diagnostics) = await GenerateWithDiagnosticsAsync("""
            using NextUnit;

            namespace TestProject;

            public class OwnTests
            {
                [Before(LifecycleScope.Test)]
                private void PrivateSetup() { }

                [Test]
                public void Run() { }
            }
            """);

        // The same rule covers the hook a class declares for itself, where the report replaces a
        // CS0122 raised inside generated code.
        Assert.True(diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT014"), FormatIds(diagnostics));
        Assert.False(registry.Contains("PrivateSetup", StringComparison.Ordinal), "PrivateSetup" + " must not be emitted");
    }

    [Fact]
    public async Task InternalBaseHookInAReferencedAssembly_ReportsNextUnit014Async()
    {
        var fixtureLibrary = await CompileLibraryAsync("Fixtures", """
            using NextUnit;

            namespace Fixtures;

            public class SharedFixture
            {
                [Before(LifecycleScope.Test)]
                internal void SharedSetup() { }
            }
            """);

        var (_, diagnostics) = await GenerateWithDiagnosticsAsync("""
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

        // Internal is in reach only inside its own assembly, or through InternalsVisibleTo.
        Assert.True(diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT014"), FormatIds(diagnostics));
    }

    [Fact]
    public async Task InaccessibleBaseHookInAScopeTheRegistryDoesNotEmit_IsNotReportedAsync()
    {
        var (_, diagnostics) = await GenerateWithDiagnosticsAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                // Assembly scope is collected from static methods only, so this hook is emitted
                // nowhere and has never failed a build.
                [Before(LifecycleScope.Assembly)]
                private void IgnoredSetup() { }
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                public void Run() { }
            }
            """);

        // NEXTUNIT014 is an error and not configurable, so reporting a hook the registry would never
        // have emitted would turn deriving from a class into a build failure for no gain.
        Assert.False(
            diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT014"),
            FormatIds(diagnostics));
    }

    [Fact]
    public async Task InaccessibleOverrideSupersededByItsBaseDeclaration_IsNotReportedAsync()
    {
        var (registry, diagnostics) = await GenerateWithDiagnosticsAsync("""
            using NextUnit;

            namespace TestProject;

            public class BaseTests
            {
                [Before(LifecycleScope.Test)]
                public virtual void Setup() { }
            }

            public class DerivedTests : BaseTests
            {
                [Before(LifecycleScope.Test)]
                protected override void Setup() { }

                [Test]
                public void Run() { }
            }
            """);

        // The base declaration wins the slot and dispatches virtually to the override, so nothing
        // calls the derived declaration directly and its accessibility never matters.
        Assert.False(
            diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT014"),
            FormatIds(diagnostics));
        Assert.Contains("((global::TestProject.BaseTests)instance).Setup()", registry);
    }

    [Fact]
    public async Task BaseClassReachableOnlyThroughAnExternAlias_ReportsNextUnit014Async()
    {
        var fixtureLibrary = await CompileLibraryAsync("Fixtures", """
            using NextUnit;

            namespace Fixtures;

            public class SharedFixture
            {
                [Before(LifecycleScope.Test)]
                public void SharedSetup() { }
            }
            """,
            alias: "fixtures");

        var (registry, diagnostics) = await GenerateWithDiagnosticsAsync("""
            extern alias fixtures;
            using NextUnit;

            namespace TestProject;

            public class DerivedTests : fixtures::Fixtures.SharedFixture
            {
                [Test]
                public void Run() { }
            }
            """,
            fixtureLibrary);

        // The type is public and C# names it fine here, but the emitted cast has to spell it
        // global::Fixtures.SharedFixture, which resolves to nothing when the reference lives only
        // under an alias. Reporting beats emitting a registry that does not compile.
        Assert.True(diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT014"), FormatIds(diagnostics));
        Assert.False(registry.Contains("SharedSetup", StringComparison.Ordinal), "the unnameable hook must not be emitted");
    }

    [Fact]
    public async Task ExplicitInterfaceHookOnABaseClass_ReportsNextUnit014Async()
    {
        var (registry, diagnostics) = await GenerateWithDiagnosticsAsync("""
            using NextUnit;

            namespace TestProject;

            public interface IFixture
            {
                void Setup();
            }

            public class BaseTests : IFixture
            {
                [Before(LifecycleScope.Test)]
                void IFixture.Setup() { }
            }

            public class DerivedTests : BaseTests
            {
                [Test]
                public void Run() { }
            }
            """);

        // The registry cannot call an explicit implementation without naming the interface, and it
        // reports Private accessibility, so the existing reachability check turns it into a report.
        // Skipping it during collection instead would drop an attributed hook without a word.
        Assert.True(diagnostics.Any(static diagnostic => diagnostic.Id == "NEXTUNIT014"), FormatIds(diagnostics));
        Assert.False(registry.Contains(".Setup()", StringComparison.Ordinal), "the uncallable hook must not be emitted");
    }

    [Fact]
    public async Task InheritedHooks_CompileAsync()
    {
        var (compilation, _) = await RunAsync("""
            using System.Threading.Tasks;
            using NextUnit;

            namespace TestProject;

            public class BaseTests<T>
            {
                [Before(LifecycleScope.Test)]
                public Task BaseBefore() => Task.CompletedTask;

                [After(LifecycleScope.Class)]
                public static void BaseAfterClass() { }
            }

            public class DerivedTests : BaseTests<string>
            {
                [Test]
                public void Run() { }
            }
            """);

        Assert.Empty(compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToList());
    }

    private static void AssertOrder(string registry, string propertyName, params string[] methodNames)
    {
        var block = ArrayBlock(registry, propertyName);
        var position = -1;

        foreach (var methodName in methodNames)
        {
            var next = block.IndexOf("." + methodName + "(", position + 1, StringComparison.Ordinal);
            Assert.True(next > position, $"'{methodName}' is missing or out of order in {propertyName}: {block}");
            position = next;
        }
    }

    /// <summary>
    /// The first emitted array for one lifecycle property, so an assertion about order cannot be
    /// satisfied by delegates belonging to a different scope.
    /// </summary>
    private static string ArrayBlock(string registry, string propertyName)
    {
        var start = registry.IndexOf(propertyName + " = ", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{propertyName} is not emitted");

        var end = registry.IndexOf("Methods = ", start + propertyName.Length + 4, StringComparison.Ordinal);
        return end < 0 ? registry.Substring(start) : registry.Substring(start, end - start);
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
        var (_, result) = await RunAsync(source, references);

        var registry = result.Results.Single().GeneratedSources
            .Single(static generated => generated.HintName == "GeneratedTestRegistry.g.cs")
            .SourceText
            .ToString();

        return (registry, result.Diagnostics);
    }

    private static async Task<(Compilation Output, GeneratorDriverRunResult Result)> RunAsync(
        string source,
        params MetadataReference[] references)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken,
            extraReferences: references);

        var driver = GeneratorDriverHarness.CreateDriver(trackIncrementalGeneratorSteps: false)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _, cancellationToken);

        return (output, driver.GetRunResult());
    }

    private static async Task<MetadataReference> CompileLibraryAsync(
        string assemblyName,
        string source,
        string? alias = null)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var compilation = await GeneratorDriverHarness.CreateCompilationAsync(
            source,
            OutputKind.DynamicallyLinkedLibrary,
            cancellationToken,
            extraReferences: null,
            assemblyName: assemblyName);

        Assert.Empty(compilation
            .GetDiagnostics(cancellationToken)
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToList());

        return alias is null
            ? compilation.ToMetadataReference()
            : compilation.ToMetadataReference(ImmutableArray.Create(alias));
    }
}
