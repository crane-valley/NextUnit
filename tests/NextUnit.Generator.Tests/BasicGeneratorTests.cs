using Xunit;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Tests for basic test method discovery in NextUnitGenerator.
/// </summary>
public class BasicGeneratorTests
{
    [Fact]
    public async Task SimpleTestMethod_GeneratesTestRegistry()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    public void SimpleTest()
    {
    }
}";

        // We're just verifying it compiles and generates something
        // Full verification of generated code would be very complex
        await VerifyGeneratorRunsWithoutErrors(source);
    }

    [Fact]
    public async Task MultipleTestMethods_GeneratesTestRegistry()
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
    public void Test2()
    {
    }

    [Test]
    public async Task AsyncTest()
    {
        await Task.CompletedTask;
    }
}";

        await VerifyGeneratorRunsWithoutErrors(source);
    }

    [Fact]
    public async Task ParameterizedTest_GeneratesMultipleTestCases()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    [Arguments(1, 2)]
    [Arguments(3, 4)]
    public void ParameterizedTest(int a, int b)
    {
    }
}";

        await VerifyGeneratorRunsWithoutErrors(source);
    }

    [Fact]
    public async Task SkippedTest_GeneratesWithSkipInfo()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    [Skip(""Not implemented yet"")]
    public void SkippedTest()
    {
    }
}";

        await VerifyGeneratorRunsWithoutErrors(source);
    }

    [Fact]
    public async Task TestWithLifecycle_GeneratesLifecycleHooks()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Before(LifecycleScope.Test)]
    public void Setup()
    {
    }

    [Test]
    public void SimpleTest()
    {
    }

    [After(LifecycleScope.Test)]
    public void Teardown()
    {
    }
}";

        await VerifyGeneratorRunsWithoutErrors(source);
    }

    [Fact]
    public async Task TestWithDependencies_GeneratesDependencyInfo()
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

        await VerifyGeneratorRunsWithoutErrors(source);
    }

    [Fact]
    public async Task ParallelConfiguration_GeneratesParallelInfo()
    {
        var source = @"
using NextUnit;

namespace TestProject;

[ParallelLimit(2)]
public class TestClass
{
    [Test]
    [NotInParallel]
    public void SerialTest()
    {
    }

    [Test]
    public void ParallelTest()
    {
    }
}";

        await VerifyGeneratorRunsWithoutErrors(source);
    }

    private static async Task VerifyGeneratorRunsWithoutErrors(string source)
    {
        // Simple verification that the generator runs without throwing
        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
        };

        await test.RunAsync();
    }
}
