namespace NextUnit.Internal;

/// <summary>
/// Represents a directed acyclic graph (DAG) of test case dependencies.
/// </summary>
public sealed class DependencyGraph
{
    /// <summary>
    /// Represents a node in the dependency graph, containing a test case and its relationships.
    /// </summary>
    public sealed class Node
    {
        /// <summary>
        /// Gets the test case descriptor associated with this node.
        /// </summary>
        public TestCaseDescriptor Test { get; }
        
        /// <summary>
        /// Gets the list of nodes that depend on this test case.
        /// </summary>
        public List<Node> Dependents { get; } = new();
        
        /// <summary>
        /// Gets or sets the number of prerequisite tests that must complete before this test can run.
        /// </summary>
        public int RemainingPrerequisites { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Node"/> class.
        /// </summary>
        /// <param name="test">The test case descriptor.</param>
        public Node(TestCaseDescriptor test)
        {
            Test = test;
        }
    }

    readonly Dictionary<TestCaseId, Node> _nodes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyGraph"/> class.
    /// </summary>
    /// <param name="nodes">The dictionary of nodes indexed by test case identifier.</param>
    DependencyGraph(Dictionary<TestCaseId, Node> nodes)
    {
        _nodes = nodes;
    }

    /// <summary>
    /// Gets the read-only collection of nodes in the dependency graph.
    /// </summary>
    public IReadOnlyDictionary<TestCaseId, Node> Nodes => _nodes;

    /// <summary>
    /// Builds a dependency graph from a collection of test case descriptors.
    /// </summary>
    /// <param name="tests">The test cases to include in the graph.</param>
    /// <returns>A new dependency graph representing the test relationships.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a test depends on a non-existent test.</exception>
    public static DependencyGraph Build(IEnumerable<TestCaseDescriptor> tests)
    {
        var nodes = tests.ToDictionary(t => t.Id, t => new Node(t));

        foreach (var node in nodes.Values)
        {
            foreach (var depId in node.Test.Dependencies)
            {
                if (!nodes.TryGetValue(depId, out var dependencyNode))
                {
                    throw new InvalidOperationException($"Missing dependency {depId.Value} for {node.Test.DisplayName}.");
                }

                dependencyNode.Dependents.Add(node);
                node.RemainingPrerequisites++;
            }
        }

        return new DependencyGraph(nodes);
    }
}
