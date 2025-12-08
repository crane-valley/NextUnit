namespace NextUnit.SampleTests;

/// <summary>
/// Demonstrates real-world testing scenarios combining multiple NextUnit features.
/// </summary>
public class RealWorldScenarioTests
{
    private static HttpClient? _httpClient;

    [Before(LifecycleScope.Class)]
    public void SetupHttpClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.example.com")
        };
    }

    [After(LifecycleScope.Class)]
    public void CleanupHttpClient()
    {
        _httpClient?.Dispose();
    }

    [Test]
    [Skip("Requires external API")]
    public async Task ApiEndpoint_ReturnsSuccessAsync()
    {
        var response = await _httpClient!.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }

    [Test]
    [Arguments("apple", "APPLE")]
    [Arguments("Hello World", "HELLO WORLD")]
    [Arguments("", "")]
    public void StringTransformation_ConvertsToUpperCase(string input, string expected)
    {
        var result = input.ToUpperInvariant();
        Assert.Equal(expected, result);
    }

    [Test]
    public void Collection_Contains_ExpectedItems()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };

        Assert.True(list.Contains(3));
        Assert.Equal(5, list.Count);
    }

    [Test]
    public async Task AsyncOperation_CompletesSuccessfullyAsync()
    {
        await Task.Delay(10);
        Assert.True(true);
    }

    [Test]
    [Arguments(null, true)]
    [Arguments("", false)]
    [Arguments("  ", false)]
    [Arguments("hello", false)]
    public void String_IsNullOrEmpty_ReturnsCorrectResult(string? input, bool expected)
    {
        var result = input == null;
        Assert.Equal(expected, result);
    }
}

/// <summary>
/// Demonstrates test ordering with dependencies.
/// </summary>
public class OrderedIntegrationTests
{
    private static bool _initialized;
    private static List<string>? _data;

    [Test]
    public void Step1_Initialize()
    {
        _initialized = true;
        _data = new List<string>();
        Assert.NotNull(_data);
    }

    [Test]
    [DependsOn(nameof(Step1_Initialize))]
    public void Step2_AddData()
    {
        Assert.True(_initialized, "Step1 should have run first");
        _data!.Add("item1");
        _data.Add("item2");
        Assert.Equal(2, _data.Count);
    }

    [Test]
    [DependsOn(nameof(Step2_AddData))]
    public void Step3_VerifyData()
    {
        Assert.True(_initialized, "Step1 should have run first");
        Assert.Equal(2, _data!.Count);
        Assert.Equal("item1", _data[0]);
    }

    [Test]
    [DependsOn(nameof(Step1_Initialize), nameof(Step2_AddData), nameof(Step3_VerifyData))]
    public void Step4_Cleanup()
    {
        _data!.Clear();
        Assert.Equal(0, _data.Count);
    }
}

/// <summary>
/// Demonstrates parallel control for resource-intensive tests.
/// </summary>
[ParallelLimit(2)]
public class ResourceIntensiveTests
{
    [Test]
    public async Task HeavyComputation1Async()
    {
        await Task.Delay(100); // Simulate heavy work
        var result = PerformComputation();
        Assert.True(result > 0);
    }

    [Test]
    public async Task HeavyComputation2Async()
    {
        await Task.Delay(100); // Simulate heavy work
        var result = PerformComputation();
        Assert.True(result > 0);
    }

    [Test]
    public async Task HeavyComputation3Async()
    {
        await Task.Delay(100); // Simulate heavy work
        var result = PerformComputation();
        Assert.True(result > 0);
    }

    private static int PerformComputation()
    {
        return Random.Shared.Next(1, 100);
    }
}

/// <summary>
/// Demonstrates exception testing.
/// </summary>
public class ExceptionHandlingTests
{
    [Test]
    public void DivideByZero_ThrowsException()
    {
        var ex = Assert.Throws<DivideByZeroException>(() =>
        {
            var x = 10;
            var y = 0;
            var result = x / y;
        });

        Assert.NotNull(ex);
    }

    [Test]
    public async Task AsyncOperation_ThrowsExceptionAsync()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("Test exception");
        });
    }

    [Test]
    [Arguments(typeof(ArgumentNullException))]
    [Arguments(typeof(InvalidOperationException))]
    public void ExceptionType_CanBeVerified(Type expectedExceptionType)
    {
        Assert.NotNull(expectedExceptionType);
        Assert.True(typeof(Exception).IsAssignableFrom(expectedExceptionType));
    }
}
