namespace NextUnit.Generator.Tests;

/// <summary>
/// Tests for basic test method discovery in NextUnitGenerator.
/// </summary>
public class BasicGeneratorTests
{
    [Fact]
    public async Task SimpleTestMethod_GeneratesTestRegistryAsync()
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
        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task MultipleTestMethods_GeneratesTestRegistryAsync()
    {
        var source = @"
using NextUnit;
using System.Threading.Tasks;

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

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task ParameterizedTest_GeneratesMultipleTestCasesAsync()
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

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task SpecialFloatingPointArguments_GenerateCompilableTestCasesAsync()
    {
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    [Arguments(float.NaN)]
    [Arguments(float.PositiveInfinity)]
    [Arguments(float.NegativeInfinity)]
    public void FloatTest(float value)
    {
    }

    [Test]
    [Arguments(double.NaN)]
    [Arguments(double.PositiveInfinity)]
    [Arguments(double.NegativeInfinity)]
    public void DoubleTest(double value)
    {
    }
}";

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task ExtremeFloatingPointArguments_GenerateCompilableTestCasesAsync()
    {
        // float.MaxValue/MinValue and double.MaxValue/MinValue must round-trip through
        // the emitted literal exactly. A bare ToString(InvariantCulture) can fall back to
        // 15/7 significant digits on some runtimes, which rounds these extreme values to
        // a string that no longer fits the type's range and fails to compile (CS0594).
        var source = @"
using NextUnit;

namespace TestProject;

public class TestClass
{
    [Test]
    [Arguments(float.MaxValue)]
    [Arguments(float.MinValue)]
    public void FloatTest(float value)
    {
    }

    [Test]
    [Arguments(double.MaxValue)]
    [Arguments(double.MinValue)]
    public void DoubleTest(double value)
    {
    }
}";

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task SkippedTest_GeneratesWithSkipInfoAsync()
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

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task TestWithLifecycle_GeneratesLifecycleHooksAsync()
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

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task TestWithDependencies_GeneratesDependencyInfoAsync()
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

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task ParallelConfiguration_GeneratesParallelInfoAsync()
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

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task TestDataAttribute_GeneratesTestDataDescriptorAsync()
    {
        var source = @"
using NextUnit;
using System.Collections.Generic;

namespace TestProject;

public class TestClass
{
    public static IEnumerable<object[]> TestCases()
    {
        yield return new object[] { 1, 2 };
        yield return new object[] { 3, 4 };
    }

    [Test]
    [TestData(nameof(TestCases))]
    public void DataDrivenTest(int a, int b)
    {
    }
}";

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task TestDataAttribute_WithMemberType_GeneratesTestDataDescriptorAsync()
    {
        var source = @"
using NextUnit;
using System.Collections.Generic;

namespace TestProject;

public static class TestDataSource
{
    public static IEnumerable<object[]> TestCases => new[]
    {
        new object[] { 1, 2 },
        new object[] { 3, 4 }
    };
}

public class TestClass
{
    [Test]
    [TestData(nameof(TestDataSource.TestCases), MemberType = typeof(TestDataSource))]
    public void DataDrivenTestWithExternalSource(int a, int b)
    {
    }
}";

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    [Fact]
    public async Task MultipleTestDataAttributes_GeneratesMultipleDescriptorsAsync()
    {
        var source = @"
using NextUnit;
using System.Collections.Generic;

namespace TestProject;

public class TestClass
{
    public static IEnumerable<object[]> Source1 => new[] { new object[] { 1 } };
    public static IEnumerable<object[]> Source2 => new[] { new object[] { 2 } };

    [Test]
    [TestData(nameof(Source1))]
    [TestData(nameof(Source2))]
    public void MultiSourceTest(int a)
    {
    }
}";

        await VerifyGeneratorRunsWithoutErrorsAsync(source);
    }

    private static async Task VerifyGeneratorRunsWithoutErrorsAsync(string source)
    {
        // Simple verification that the generator runs without throwing
        var test = new CSharpSourceGeneratorVerifier<NextUnitGenerator>.Test
        {
            TestCode = source,
            // Skip verifying generated source files, we just want to verify no errors
            TestBehaviors = Microsoft.CodeAnalysis.Testing.TestBehaviors.SkipGeneratedSourcesCheck,
        };

        await test.RunAsync();
    }
}
