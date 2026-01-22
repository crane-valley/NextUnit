namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating parallel execution control.
/// </summary>
public class ParallelTests
{
    private static int _concurrentCount;
    private static readonly object _lock = new();

    [Test]
    public async Task ParallelTest1Async()
    {
        await SimulateWorkAsync();
    }

    [Test]
    public async Task ParallelTest2Async()
    {
        await SimulateWorkAsync();
    }

    [Test]
    public async Task ParallelTest3Async()
    {
        await SimulateWorkAsync();
    }

    private static async Task SimulateWorkAsync()
    {
        lock (_lock)
        {
            _concurrentCount++;
        }

        await Task.Delay(100);

        lock (_lock)
        {
            _concurrentCount--;
        }
    }
}

/// <summary>
/// Tests that must run serially.
/// </summary>
[NotInParallel]
public class SerialTests
{
    private int _runningTests;

    [Test]
    public async Task SerialTest1Async()
    {
        _runningTests++;
        Assert.Equal(1, _runningTests);
        await Task.Delay(50);
        _runningTests--;
    }

    [Test]
    public async Task SerialTest2Async()
    {
        _runningTests++;
        Assert.Equal(1, _runningTests);
        await Task.Delay(50);
        _runningTests--;
    }
}

/// <summary>
/// Tests with parallel limit.
/// </summary>
[ParallelLimit(2)]
public class LimitedParallelTests
{
    [Test]
    public async Task LimitedTest1Async() => await Task.Delay(50);

    [Test]
    public async Task LimitedTest2Async() => await Task.Delay(50);

    [Test]
    public async Task LimitedTest3Async() => await Task.Delay(50);

    [Test]
    public async Task LimitedTest4Async() => await Task.Delay(50);
}
