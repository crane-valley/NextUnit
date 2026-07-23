using BenchmarkDotNet.Attributes;
using NextUnit.Internal;

namespace NextUnit.Benchmarks;

[MemoryDiagnoser]
public class ParallelSchedulerBenchmarks
{
    private DependencyGraph _graph = null!;

    [Params(100, 1_000, 10_000)]
    public int TestCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var tests = Enumerable.Range(0, TestCount)
            .Select(static index => new TestCaseDescriptor
            {
                Id = new TestCaseId($"SchedulerTests.Test{index}"),
                DisplayName = $"Test{index}",
                Parallel = new ParallelInfo { ParallelLimit = 8 }
            })
            .ToArray();

        _graph = DependencyGraph.Build(tests);
    }

    [Benchmark]
    public async Task<int> CreateExecutionBatchesAsync()
    {
        var batchCount = 0;
        var scheduler = new ParallelScheduler(_graph);
        await foreach (var _ in scheduler.GetExecutionBatchesAsync(CancellationToken.None))
        {
            batchCount++;
        }

        return batchCount;
    }
}
