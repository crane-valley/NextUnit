using System.Runtime.CompilerServices;

namespace NextUnit.Internal;

/// <summary>
/// Schedules and coordinates the parallel execution of test cases based on dependencies and parallel execution constraints.
/// </summary>
public sealed class ParallelScheduler
{
    readonly DependencyGraph _graph;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParallelScheduler"/> class.
    /// </summary>
    /// <param name="graph">The dependency graph containing test cases and their relationships.</param>
    public ParallelScheduler(DependencyGraph graph)
    {
        _graph = graph;
    }

    /// <summary>
    /// Gets an asynchronous stream of test cases in the order they should be executed, respecting dependencies and parallelism constraints.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>An asynchronous enumerable of test case descriptors in execution order.</returns>
    public async IAsyncEnumerable<TestCaseDescriptor> GetExecutionOrderAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // TODO: Simple implementation considering only dependencies, not parallelism
        // Parallel execution constraints will be enforced in M3
        var remainingDeps = _graph.Nodes.Values.ToDictionary(
            n => n.Test.Id,
            n => n.RemainingPrerequisites);

        var ready = new Queue<DependencyGraph.Node>(
            _graph.Nodes.Values.Where(n => remainingDeps[n.Test.Id] == 0));

        var completed = new HashSet<TestCaseId>();

        while (ready.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var node = ready.Dequeue();

            if (ShouldSkipDueToDependencies(node, completed))
            {
                continue;
            }

            yield return node.Test;
            completed.Add(node.Test.Id);

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

    /// <summary>
    /// Determines whether a test should be skipped due to failed or skipped dependencies.
    /// </summary>
    /// <param name="node">The test node to check.</param>
    /// <param name="completed">The set of completed test identifiers.</param>
    /// <returns><c>true</c> if the test should be skipped; otherwise, <c>false</c>.</returns>
    static bool ShouldSkipDueToDependencies(DependencyGraph.Node node, HashSet<TestCaseId> completed)
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
