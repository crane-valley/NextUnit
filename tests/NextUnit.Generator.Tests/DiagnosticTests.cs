using Microsoft.CodeAnalysis;
using Xunit;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Tests for diagnostic reporting in NextUnitGenerator.
/// </summary>
public class DiagnosticTests
{
    [Fact]
    public async Task CircularDependency_ReportsError()
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
        };

        // Expect NEXTUNIT001 diagnostic for circular dependency
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NEXTUNIT001", DiagnosticSeverity.Error)
        );

        await test.RunAsync();
    }

    [Fact]
    public async Task UnresolvedDependency_ReportsWarning()
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
        };

        // Expect NEXTUNIT002 diagnostic for unresolved dependency
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NEXTUNIT002", DiagnosticSeverity.Warning)
        );

        await test.RunAsync();
    }

    [Fact]
    public async Task ValidDependency_NoWarning()
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
        };

        // No diagnostics expected
        await test.RunAsync();
    }

    [Fact]
    public async Task SelfDependency_ReportsError()
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
        };

        // Expect NEXTUNIT001 diagnostic for self-dependency (cycle)
        test.ExpectedDiagnostics.Add(
            new DiagnosticResult("NEXTUNIT001", DiagnosticSeverity.Error)
        );

        await test.RunAsync();
    }
}
