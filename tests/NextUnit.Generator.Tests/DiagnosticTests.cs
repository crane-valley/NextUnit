using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Tests for diagnostic reporting in NextUnitGenerator.
/// </summary>
public class DiagnosticTests
{
    [Fact]
    public async Task CircularDependency_ReportsErrorAsync()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    [DependsOn(nameof(Test2))]
    public void Test1()
    {
    }

    [Test]
    [DependsOn(nameof(Test1))]
    public void Test2()
    {
    }
}";

        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            TestBehaviors = Microsoft.CodeAnalysis.Testing.TestBehaviors.SkipGeneratedSourcesCheck,
        };

        // Expect NEXTUNIT001 diagnostic for circular dependency
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NEXTUNIT001", DiagnosticSeverity.Error)
        );
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NEXTUNIT001", DiagnosticSeverity.Error)
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnresolvedDependency_ReportsWarningAsync()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    [DependsOn(""NonExistentTest"")]
    public void Test1()
    {
    }
}";

        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            TestBehaviors = Microsoft.CodeAnalysis.Testing.TestBehaviors.SkipGeneratedSourcesCheck,
        };

        // Expect NEXTUNIT002 diagnostic for unresolved dependency
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NEXTUNIT002", DiagnosticSeverity.Warning)
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ValidDependency_NoWarningAsync()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    public void Test1()
    {
    }

    [Test]
    [DependsOn(nameof(Test1))]
    public void Test2()
    {
    }
}";

        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            TestBehaviors = Microsoft.CodeAnalysis.Testing.TestBehaviors.SkipGeneratedSourcesCheck,
        };

        // No diagnostics expected
        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SelfDependency_ReportsErrorAsync()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    [DependsOn(nameof(Test1))]
    public void Test1()
    {
    }
}";

        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            TestBehaviors = Microsoft.CodeAnalysis.Testing.TestBehaviors.SkipGeneratedSourcesCheck,
        };

        // Expect NEXTUNIT001 diagnostic for self-dependency (cycle)
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NEXTUNIT001", DiagnosticSeverity.Error)
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// NEXTUNIT009 is not gated on the partition the way NU0022 is. The parameter-level source
    /// shadows the class source, so nothing expands it and nothing roots it, and the keyed
    /// declaration is still an error: it judges what the user wrote, not what the registry emitted.
    /// </summary>
    [Fact]
    public async Task KeyedClassDataSourceWithoutKey_ShadowedByParameterSource_ReportsErrorAsync()
    {
        var source = @"
using NextUnit;
using System.Collections;
using System.Collections.Generic;

namespace TestProject;

public sealed class Rows : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator() => throw new System.NotImplementedException();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class TestClass
{
    [Test]
    [ClassDataSource<Rows>(Shared = SharedType.Keyed)]
    public void Test1([Values(1, 2)] int value)
    {
    }
}";

        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            TestBehaviors = Microsoft.CodeAnalysis.Testing.TestBehaviors.SkipGeneratedSourcesCheck,
        };

        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NEXTUNIT009", DiagnosticSeverity.Error)
        );
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NEXTUNIT010", DiagnosticSeverity.Warning)
        );

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
