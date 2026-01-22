using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace NextUnit.Internal;

/// <summary>
/// Schedules and coordinates the parallel execution of test cases based on dependencies and parallel execution constraints.
/// </summary>
public sealed class ParallelScheduler
{
    private readonly DependencyGraph _graph;
    private readonly int _globalMaxDegreeOfParallelism;
    private readonly ConcurrentDictionary<TestCaseId, TestOutcome> _outcomes = new();

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
    /// Reports the outcome of a test execution. Used to determine whether dependent tests should be skipped.
    /// </summary>
    /// <param name="testId">The test case identifier.</param>
    /// <param name="outcome">The test outcome.</param>
    public void ReportOutcome(TestCaseId testId, TestOutcome outcome)
    {
        _outcomes.TryAdd(testId, outcome);
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

            // Collect all tests that are ready to run, separating those to skip
            var readyTests = new List<DependencyGraph.Node>();
            var testsToSkip = new List<DependencyGraph.Node>();
            while (ready.Count > 0)
            {
                var node = ready.Dequeue();
                if (!AreDependenciesComplete(node, completed))
                {
                    // Dependencies not complete yet, can't run - shouldn't happen as we check remainingDeps
                    continue;
                }
                if (ShouldSkipDueToFailedDependencies(node))
                {
                    testsToSkip.Add(node);
                }
                else
                {
                    readyTests.Add(node);
                }
            }

            // Handle tests that should be skipped due to failed dependencies
            // Mark them as completed and decrement dependents so they can be evaluated
            foreach (var node in testsToSkip)
            {
                // Report as skipped and mark completed
                ReportOutcome(node.Test.Id, TestOutcome.Skipped);
                completed.Add(node.Test.Id);

                // Decrement remaining deps for dependents
                foreach (var dependent in node.Dependents)
                {
                    remainingDeps[dependent.Test.Id]--;
                    if (remainingDeps[dependent.Test.Id] == 0)
                    {
                        ready.Enqueue(dependent);
                    }
                }
            }

            // Yield skip batch if any tests need to be reported as skipped
            if (testsToSkip.Count > 0)
            {
                yield return new TestBatch
                {
                    Tests = testsToSkip.Select(n => n.Test.WithSkipReason("Dependency failed")).ToList(),
                    MaxDegreeOfParallelism = 1,
                    IsSerial = true,
                    IsSkipBatch = true,
                    ParallelGroup = null,
                    ConstraintKeys = Array.Empty<string>()
                };
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

        // First, separate tests by parallel group
        var groupedTests = readyTests
            .Where(n => n.Test.Parallel.ParallelGroup is not null)
            .GroupBy(n => n.Test.Parallel.ParallelGroup!)
            .ToList();

        var ungroupedTests = readyTests
            .Where(n => n.Test.Parallel.ParallelGroup is null)
            .ToList();

        // Create batches for each parallel group (group runs exclusively)
        foreach (var group in groupedTests)
        {
            var groupTests = group.Select(n => n.Test).ToList();
            var limit = groupTests.Min(t => t.Parallel.ParallelLimit) ?? _globalMaxDegreeOfParallelism;

            batches.Add(new TestBatch
            {
                Tests = groupTests,
                MaxDegreeOfParallelism = limit,
                IsSerial = false,
                ParallelGroup = group.Key,
                ConstraintKeys = Array.Empty<string>()
            });
        }

        // Handle ungrouped tests
        // Separate by constraint keys and NotInParallel flag
        var serialWithoutKeys = ungroupedTests
            .Where(n => n.Test.Parallel.NotInParallel && n.Test.Parallel.ConstraintKeys.Count == 0)
            .ToList();

        var serialWithKeys = ungroupedTests
            .Where(n => n.Test.Parallel.NotInParallel && n.Test.Parallel.ConstraintKeys.Count > 0)
            .ToList();

        var parallelTests = ungroupedTests
            .Where(n => !n.Test.Parallel.NotInParallel)
            .ToList();

        // Create serial batches for tests without constraint keys (one test per batch)
        foreach (var node in serialWithoutKeys)
        {
            batches.Add(new TestBatch
            {
                Tests = new[] { node.Test },
                MaxDegreeOfParallelism = 1,
                IsSerial = true,
                ParallelGroup = null,
                ConstraintKeys = Array.Empty<string>()
            });
        }

        // Group tests with constraint keys by their keys
        // Tests sharing any constraint key cannot run in parallel
        var constraintKeyGroups = GroupByConstraintKeys(serialWithKeys);
        foreach (var constraintGroup in constraintKeyGroups)
        {
            // Each constraint group runs one test at a time
            foreach (var node in constraintGroup.Nodes)
            {
                batches.Add(new TestBatch
                {
                    Tests = new[] { node.Test },
                    MaxDegreeOfParallelism = 1,
                    IsSerial = true,
                    ParallelGroup = null,
                    ConstraintKeys = constraintGroup.Keys.ToArray()
                });
            }
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
                    IsSerial = false,
                    ParallelGroup = null,
                    ConstraintKeys = Array.Empty<string>()
                });
            }
        }

        return batches;
    }

    /// <summary>
    /// Groups tests by constraint keys using union-find to merge tests sharing any key.
    /// </summary>
    private static List<ConstraintKeyGroup> GroupByConstraintKeys(List<DependencyGraph.Node> tests)
    {
        if (tests.Count == 0)
        {
            return new List<ConstraintKeyGroup>();
        }

        // Build a mapping of constraint key to test indices
        var keyToIndices = new Dictionary<string, List<int>>();
        for (var i = 0; i < tests.Count; i++)
        {
            foreach (var key in tests[i].Test.Parallel.ConstraintKeys)
            {
                if (!keyToIndices.TryGetValue(key, out var indices))
                {
                    indices = new List<int>();
                    keyToIndices[key] = indices;
                }
                indices.Add(i);
            }
        }

        // Union-find to group tests sharing any constraint key
        var parent = new int[tests.Count];
        for (var i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }

        int Find(int x)
        {
            if (parent[x] != x)
            {
                parent[x] = Find(parent[x]);
            }
            return parent[x];
        }

        void Union(int x, int y)
        {
            var px = Find(x);
            var py = Find(y);
            if (px != py)
            {
                parent[px] = py;
            }
        }

        // Union tests that share any constraint key
        foreach (var indices in keyToIndices.Values)
        {
            for (var i = 1; i < indices.Count; i++)
            {
                Union(indices[0], indices[i]);
            }
        }

        // Group tests by their root parent
        var groups = new Dictionary<int, (HashSet<string> Keys, List<DependencyGraph.Node> Nodes)>();
        for (var i = 0; i < tests.Count; i++)
        {
            var root = Find(i);
            if (!groups.TryGetValue(root, out var group))
            {
                group = (new HashSet<string>(), new List<DependencyGraph.Node>());
                groups[root] = group;
            }
            group.Nodes.Add(tests[i]);
            foreach (var key in tests[i].Test.Parallel.ConstraintKeys)
            {
                group.Keys.Add(key);
            }
        }

        return groups.Values.Select(g => new ConstraintKeyGroup(g.Keys, g.Nodes)).ToList();
    }

    /// <summary>
    /// Checks whether all dependencies of a test have completed.
    /// </summary>
    /// <param name="node">The test node to check.</param>
    /// <param name="completed">The set of completed test identifiers.</param>
    /// <returns><c>true</c> if all dependencies have completed; otherwise, <c>false</c>.</returns>
    private static bool AreDependenciesComplete(DependencyGraph.Node node, HashSet<TestCaseId> completed)
    {
        foreach (var depInfo in node.Test.DependencyInfos)
        {
            if (!completed.Contains(depInfo.DependsOnId))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Determines whether a test should be skipped because a dependency failed or was skipped.
    /// </summary>
    /// <param name="node">The test node to check.</param>
    /// <returns><c>true</c> if the test should be skipped due to failed dependencies; otherwise, <c>false</c>.</returns>
    private bool ShouldSkipDueToFailedDependencies(DependencyGraph.Node node)
    {
        foreach (var depInfo in node.Test.DependencyInfos)
        {
            if (!depInfo.ProceedOnFailure &&
                _outcomes.TryGetValue(depInfo.DependsOnId, out var outcome) &&
                outcome != TestOutcome.Passed)
            {
                return true;
            }
        }
        return false;
    }

    private sealed class ConstraintKeyGroup
    {
        public ConstraintKeyGroup(HashSet<string> keys, List<DependencyGraph.Node> nodes)
        {
            Keys = keys;
            Nodes = nodes;
        }

        public HashSet<string> Keys { get; }
        public List<DependencyGraph.Node> Nodes { get; }
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

    /// <summary>
    /// Gets or initializes a value indicating whether this batch contains tests that should be
    /// reported as skipped due to failed dependencies.
    /// </summary>
    public bool IsSkipBatch { get; init; }

    /// <summary>
    /// Gets or initializes the parallel group name for this batch, if any.
    /// </summary>
    public required string? ParallelGroup { get; init; }

    /// <summary>
    /// Gets or initializes the constraint keys that apply to this batch.
    /// </summary>
    public required IReadOnlyList<string> ConstraintKeys { get; init; }
}
