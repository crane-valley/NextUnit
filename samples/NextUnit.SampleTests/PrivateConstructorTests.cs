using NextUnit.Core;

namespace NextUnit.SampleTests;

/// <summary>
/// Tests to verify that private constructors with ITestOutput don't cause issues.
/// This ensures the generator only checks public constructors when detecting ITestOutput requirements.
/// </summary>
public class PrivateConstructorTests
{
    // Public parameterless constructor (will be used by test framework)
    public PrivateConstructorTests()
    {
    }

    // Private constructor with ITestOutput - should NOT trigger RequiresTestOutput
#pragma warning disable IDE0051 // Remove unused private members - intentionally unused for testing
    private PrivateConstructorTests(ITestOutput output)
    {
        // This constructor should be ignored by the generator
    }
#pragma warning restore IDE0051

    [Test]
    public void TestWithPublicParameterlessConstructor()
    {
        // This test should work fine because the public parameterless constructor exists
        Assert.True(true);
    }

    [Test]
    public void AnotherTestVerifyingNoIssue()
    {
        var result = 1 + 1;
        Assert.Equal(2, result);
    }
}
