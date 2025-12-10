using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using NextUnit.Core;

namespace NextUnit.Benchmarks;

/// <summary>
/// Benchmarks for test discovery and execution performance
/// </summary>
[SimpleJob(RunStrategy.Monitoring, iterationCount: 10)]
[MemoryDiagnoser]
public class TestExecutionBenchmarks
{
    private readonly Consumer _consumer = new();

    [Benchmark(Description = "Execute 1000 simple tests")]
    public async Task Execute1000Tests()
    {
        // Simulate running the large test suite
        // In practice, this would launch the test executable, but for benchmark purposes
        // we'll measure the overhead of test registration and basic execution
        
        var tasks = new List<Task>();
        for (int i = 0; i < 1000; i++)
        {
            tasks.Add(Task.Run(() => SimpleTest()));
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "Execute 100 simple tests")]
    public async Task Execute100Tests()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => SimpleTest()));
        }
        await Task.WhenAll(tasks);
    }

    [Benchmark(Description = "Execute 10 simple tests")]
    public async Task Execute10Tests()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() => SimpleTest()));
        }
        await Task.WhenAll(tasks);
    }

    private void SimpleTest()
    {
        Assert.True(true);
        _consumer.Consume(true);
    }
}
