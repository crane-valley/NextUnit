using System.Runtime.CompilerServices;

namespace NextUnit.Internal;

/// <summary>
/// Schedules and coordinates the parallel execution of test cases based on dependencies and parallel execution constraints.
/// </summary>
public sealed class ParallelScheduler
{
    private readonly DependencyGraph _graph;
    private readonly int _globalMaxDegreeOfParallelism;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelScheduler"/> class.
    /// </summary>
    /// <param name="graph">The dependency graph containing test cases and their relationships.</param>
    /// <param name="maxDegreeOfParallelism">The maximum number of tests that can run in parallel globally. Defaults to processor count.</param>
    public ParallelScheduler(DependencyGraph graph, int? maxDegreeOfParallelism = null)
    {
        _graph = graph;
        _globalMaxDegreeOfParallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;
    }

    /// <summary>
    /// Gets an asynchronous stream of test batches ready for parallel execution.
    /// Each batch contains tests that can be executed in parallel.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>An asynchronous enumerable of test batch descriptors.</returns>
    public async IAsyncEnumerable<TestBatch> GetExecutionBatchesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var remainingDeps = _graph.Nodes.Values.ToDictionary(
            n => n.Test.Id,
            n => n.RemainingPrerequisites);

        var ready = new Queue<DependencyGraph.Node>(
            _graph.Nodes.Values.Where(n => remainingDeps[n.Test.Id] == 0));

        var completed = new HashSet<TestCaseId>();

        while (ready.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Collect all tests that are ready to run
            var readyTests = new List<DependencyGraph.Node>();
            while (ready.Count > 0)
            {
                var node = ready.Dequeue();
                if (!ShouldSkipDueToDependencies(node, completed))
                {
                    readyTests.Add(node);
                }
            }

            if (readyTests.Count == 0)
            {
                continue;
            }

            // Group tests into batches based on parallel constraints
            var batches = GroupIntoBatches(readyTests);

            foreach (var batch in batches)
            {
                yield return batch;

                // Mark all tests in this batch as completed
                foreach (var test in batch.Tests)
                {
                    completed.Add(test.Id);

                    // Find the node for this test and enqueue its dependents
                    var node = _graph.Nodes[test.Id];
                    foreach (var dependent in node.Dependents)
                    {
                        remainingDeps[dependent.Test.Id]--;
                        if (remainingDeps[dependent.Test.Id] == 0)
                        {
                            ready.Enqueue(dependent);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Groups ready tests into batches based on parallel execution constraints.
    /// </summary>
    /// <param name="readyTests">The list of tests ready for execution.</param>
    /// <returns>A list of test batches.</returns>
    private List<TestBatch> GroupIntoBatches(List<DependencyGraph.Node> readyTests)
    {
        var batches = new List<TestBatch>();

        // Separate tests that must run serially
        var serialTests = readyTests.Where(n => n.Test.Parallel.NotInParallel).ToList();
        var parallelTests = readyTests.Where(n => !n.Test.Parallel.NotInParallel).ToList();

        // Create serial batches (one test per batch)
        foreach (var node in serialTests)
        {
            batches.Add(new TestBatch
            {
                Tests = new[] { node.Test },
                MaxDegreeOfParallelism = 1,
                IsSerial = true
            });
        }

        // Group parallel tests by their ParallelLimit
        var testsByLimit = parallelTests
            .GroupBy(n => n.Test.Parallel.ParallelLimit ?? _globalMaxDegreeOfParallelism)
            .ToList();

        foreach (var group in testsByLimit)
        {
            var limit = group.Key;
            var tests = group.Select(n => n.Test).ToList();

            // If there are many tests with the same limit, we can potentially split them
            // into multiple batches for better load balancing
            while (tests.Count > 0)
            {
                var batchSize = Math.Min(tests.Count, limit * 2); // Allow some buffering
                var batchTests = tests.Take(batchSize).ToList();
                tests = tests.Skip(batchSize).ToList();

                batches.Add(new TestBatch
                {
                    Tests = batchTests,
                    MaxDegreeOfParallelism = limit,
                    IsSerial = false
                });
            }
        }

        return batches;
    }

    /// <summary>
    /// Determines whether a test should be skipped due to failed or skipped dependencies.
    /// </summary>
    /// <param name="node">The test node to check.</param>
    /// <param name="completed">The set of completed test identifiers.</param>
    /// <returns><c>true</c> if the test should be skipped; otherwise, <c>false</c>.</returns>
    private static bool ShouldSkipDueToDependencies(DependencyGraph.Node node, HashSet<TestCaseId> completed)
    {
        // Check if all dependencies have completed
        foreach (var depId in node.Test.Dependencies)
        {
            if (!completed.Contains(depId))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Represents a batch of tests that can be executed in parallel.
/// </summary>
public sealed class TestBatch
{
    /// <summary>
    /// Gets or initializes the tests in this batch.
    /// </summary>
    public required IReadOnlyList<TestCaseDescriptor> Tests { get; init; }

    /// <summary>
    /// Gets or initializes the maximum degree of parallelism for this batch.
    /// </summary>
    public required int MaxDegreeOfParallelism { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether this batch must be executed serially.
    /// </summary>
    public required bool IsSerial { get; init; }
}
