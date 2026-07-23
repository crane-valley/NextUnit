using NextUnit.Internal;

namespace NextUnit.Platform.Tests;

public sealed class ParallelSchedulerTests
{
    [Test]
    public async Task SameParallelLimit_UsesSingleWorkStealingBatchAsync()
    {
        var tests = CreateTests(10_000, parallelLimit: 20);
        var scheduler = new ParallelScheduler(DependencyGraph.Build(tests), maxDegreeOfParallelism: 20);

        var batches = await CollectBatchesAsync(scheduler);

        var batch = Assert.Single(batches);
        Assert.Equal(10_000, batch.Tests.Count);
        Assert.Equal(20, batch.MaxDegreeOfParallelism);
        Assert.False(batch.IsSerial);
    }

    [Test]
    public async Task GlobalSerialTests_UseSingleSerialBatchAsync()
    {
        var tests = CreateTests(1_000, notInParallel: true);
        var scheduler = new ParallelScheduler(DependencyGraph.Build(tests));

        var batches = await CollectBatchesAsync(scheduler);

        var batch = Assert.Single(batches);
        Assert.Equal(1_000, batch.Tests.Count);
        Assert.Equal(1, batch.MaxDegreeOfParallelism);
        Assert.True(batch.IsSerial);
    }

    [Test]
    public async Task ConstraintGroup_UsesSinglePriorityOrderedBatchAsync()
    {
        var tests = new[]
        {
            CreateTest("low", priority: -1, constraintKeys: ["database"]),
            CreateTest("high", priority: 10, constraintKeys: ["database"]),
            CreateTest("normal", priority: 0, constraintKeys: ["database"])
        };
        var scheduler = new ParallelScheduler(DependencyGraph.Build(tests));

        var batches = await CollectBatchesAsync(scheduler);

        var batch = Assert.Single(batches);
        Assert.True(batch.IsSerial);
        Assert.Equal(
            new[] { "high", "normal", "low" },
            batch.Tests.Select(static test => test.Id.Value).ToArray());
    }

    private static List<TestCaseDescriptor> CreateTests(
        int count,
        int? parallelLimit = null,
        bool notInParallel = false)
    {
        var tests = new List<TestCaseDescriptor>(count);
        for (var index = 0; index < count; index++)
        {
            tests.Add(CreateTest(
                $"test.{index}",
                parallelLimit: parallelLimit,
                notInParallel: notInParallel));
        }

        return tests;
    }

    private static TestCaseDescriptor CreateTest(
        string id,
        int priority = 0,
        int? parallelLimit = null,
        bool notInParallel = false,
        IReadOnlyList<string>? constraintKeys = null) =>
        new()
        {
            Id = new TestCaseId(id),
            DisplayName = id,
            TestClass = typeof(ParallelSchedulerTests),
            MethodName = nameof(CreateTest),
            TestMethod = static (_, _) => Task.CompletedTask,
            Priority = priority,
            Parallel = new ParallelInfo
            {
                ParallelLimit = parallelLimit,
                NotInParallel = notInParallel || constraintKeys is { Count: > 0 },
                ConstraintKeys = constraintKeys ?? Array.Empty<string>()
            }
        };

    private static async Task<List<TestBatch>> CollectBatchesAsync(ParallelScheduler scheduler)
    {
        var batches = new List<TestBatch>();
        await foreach (var batch in scheduler.GetExecutionBatchesAsync(CancellationToken.None))
        {
            batches.Add(batch);
        }

        return batches;
    }
}
