using NextUnit.Core;

namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating test output functionality.
/// </summary>
public class TestOutputTests
{
    private readonly ITestOutput _output;

    public TestOutputTests(ITestOutput output)
    {
        _output = output;
    }

    [Test]
    public void TestWithSimpleOutput()
    {
        _output.WriteLine("Starting test execution...");
        _output.WriteLine("Performing some operation");

        var result = 2 + 2;
        _output.WriteLine($"Result: {result}");

        Assert.Equal(4, result);
        _output.WriteLine("Test completed successfully!");
    }

    [Test]
    public void TestWithFormattedOutput()
    {
        _output.WriteLine("Test started at: {0}", DateTime.UtcNow);

        var values = new[] { 1, 2, 3, 4, 5 };
        _output.WriteLine("Processing {0} values", values.Length);

        var sum = values.Sum();
        _output.WriteLine("Sum of values: {0}", sum);

        Assert.Equal(15, sum);
    }

    [Test]
    public void TestWithMultilineOutput()
    {
        _output.WriteLine("Step 1: Initialize");
        var data = new List<string> { "apple", "banana", "cherry" };

        _output.WriteLine("Step 2: Process items");
        foreach (var item in data)
        {
            _output.WriteLine("  - Processing: {0}", item);
        }

        _output.WriteLine("Step 3: Verify count");
        Assert.Equal(3, data.Count);

        _output.WriteLine("All steps completed");
    }

    [Test]
    [Arguments(1, 2, 3)]
    [Arguments(10, 20, 30)]
    public void ParameterizedTestWithOutput(int a, int b, int expected)
    {
        _output.WriteLine("Testing addition: {0} + {1}", a, b);

        var result = a + b;
        _output.WriteLine("Result: {0}", result);

        Assert.Equal(expected, result);
    }

    [Test]
    public async Task AsyncTestWithOutputAsync()
    {
        _output.WriteLine("Starting async operation...");

        await Task.Delay(10);
        _output.WriteLine("Async delay completed");

        var result = await Task.FromResult(42);
        _output.WriteLine("Async result: {0}", result);

        Assert.Equal(42, result);
    }

    [Test]
    [Skip("Intentionally failing test to demonstrate output capture - skipped to prevent CI failures")]
    public void TestOutputInFailedTest()
    {
        _output.WriteLine("This output should be visible even when test fails");
        _output.WriteLine("Attempting operation that will fail...");

        // This will fail and output should still be captured
        Assert.Equal(5, 10);
    }
}
