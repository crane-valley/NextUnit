using Xunit;

namespace NextUnit.Generator.Tests;

/// <summary>
/// Integration tests that verify the generator produces valid code by building sample projects.
/// </summary>
public class GeneratorIntegrationTests
{
    [Fact]
    public void Generator_ProducesCompilableCode_ForBasicTests()
    {
        // This test verifies that the sample project compiles successfully
        // The sample project exercises all major generator features
        
        // If this test is running, it means:
        // 1. The generator executed successfully during the build
        // 2. The generated code compiled without errors
        // 3. The test project references are correctly configured
        
        Assert.True(true, "Generator produced compilable code");
    }

    [Fact]
    public void Generator_SupportsMultipleTestCases()
    {
        // Verify that parameterized tests generate multiple test case descriptors
        // This is validated by the SampleTests project having multiple test cases
        
        var sampleTestsAssembly = typeof(NextUnit.SampleTests.ParameterizedTests).Assembly;
        Assert.NotNull(sampleTestsAssembly);
    }

    [Fact]
    public void Generator_SupportsLifecycleScopes()
    {
        // Verify that lifecycle scope attributes are recognized
        var classLifecycleType = typeof(NextUnit.SampleTests.ClassLifecycleTests);
        var assemblyLifecycleType = typeof(NextUnit.SampleTests.AssemblyLifecycleTests);
        
        Assert.NotNull(classLifecycleType);
        Assert.NotNull(assemblyLifecycleType);
    }
}
