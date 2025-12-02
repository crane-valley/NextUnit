using NextUnit;

namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating parallel execution control.
/// </summary>
public class ParallelTests
{
    static int _concurrentCount;
    static readonly object _lock = new();

    [Test]
    public async Task ParallelTest1()
    {
        await SimulateWork();
    }

    [Test]
    public async Task ParallelTest2()
    {
        await SimulateWork();
    }

    [Test]
    public async Task ParallelTest3()
    {
        await SimulateWork();
    }

    static async Task SimulateWork()
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
    static int _runningTests;

    [Test]
    public async Task SerialTest1()
    {
        _runningTests++;
        Assert.Equal(1, _runningTests);
        await Task.Delay(50);
        _runningTests--;
    }

    [Test]
    public async Task SerialTest2()
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
    public async Task LimitedTest1() => await Task.Delay(50);

    [Test]
    public async Task LimitedTest2() => await Task.Delay(50);

    [Test]
    public async Task LimitedTest3() => await Task.Delay(50);

    [Test]
    public async Task LimitedTest4() => await Task.Delay(50);
}
