using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using NextUnit.Internal;

namespace NextUnit.Benchmarks;

/// <summary>
/// Measures what deferred <c>[TestData]</c> enumeration is supposed to buy: discovery that does not
/// grow with the size of the data source, at the cost of paying for the rows during execution.
/// </summary>
/// <remarks>
/// The three benchmarks split the work the way the runtime does. Eager discovery is what happens
/// today for every source; deferred discovery is what replaces it when the option is set; deferred
/// execution fan-out is the cost that moves. Comparing the first against the sum of the last two
/// shows that deferral moves work rather than removing it, which is the honest claim.
/// <para>
/// The data source hands out one materialized row array per call so that the measurement covers the
/// expander, not the cost of fabricating rows. Row construction still allocates, which is why the
/// eager and fan-out numbers are close: the point of the comparison is the discovery column.
/// </para>
/// </remarks>
[SimpleJob(RunStrategy.Monitoring, iterationCount: 10)]
[MemoryDiagnoser]
public class DataSourceExpansionBenchmarks
{
    private object[][] _rows = [];
    private TestDataDescriptor _eagerDescriptor = null!;
    private TestDataDescriptor _deferredDescriptor = null!;
    private TestDataDescriptor _deferredAsyncDescriptor = null!;

    [Params(100, 1_000, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _rows = Enumerable.Range(0, RowCount)
            .Select(static index => new object[] { index, index, index * 2 })
            .ToArray();

        _eagerDescriptor = CreateDescriptor(deferred: false);
        _deferredDescriptor = CreateDescriptor(deferred: true);
        _deferredAsyncDescriptor = CreateAsyncDescriptor();
    }

    /// <summary>
    /// The default: every row becomes a test case before discovery returns.
    /// </summary>
    [Benchmark(Description = "Discovery: eager expansion of every row", Baseline = true)]
    public async Task<int> EagerDiscoveryAsync()
    {
        var testCases = await TestDataExpander.ExpandAsync([_eagerDescriptor], CancellationToken.None);
        return testCases.Count;
    }

    /// <summary>
    /// The opt-in: discovery produces one placeholder and never touches the member, so this column
    /// must stay flat across every <see cref="RowCount"/>.
    /// </summary>
    [Benchmark(Description = "Discovery: deferred placeholder only")]
    public async Task<int> DeferredDiscoveryAsync()
    {
        var testCases = await TestDataExpander.ExpandAsync([_deferredDescriptor], CancellationToken.None);
        return testCases.Count;
    }

    /// <summary>
    /// The cost that moved: the execution engine expanding the placeholder before it builds the
    /// dependency graph.
    /// </summary>
    [Benchmark(Description = "Execution: deferred fan-out into rows")]
    public async Task<int> DeferredExecutionFanOutAsync()
    {
        var testCases = await TestDataExpander.ExpandDeferredAsync(_deferredDescriptor, CancellationToken.None);
        return testCases.Count;
    }

    /// <summary>
    /// The asynchronous fan-out is measured separately because it is the path that could hold two
    /// collections proportional to the source: rows are drained through an enumerator rather than
    /// handed over as a ready collection, so it has to project each row as it arrives instead of
    /// materializing first and mapping afterwards.
    /// </summary>
    [Benchmark(Description = "Execution: deferred async fan-out into rows")]
    public async Task<int> DeferredAsyncExecutionFanOutAsync()
    {
        var testCases = await TestDataExpander.ExpandDeferredAsync(_deferredAsyncDescriptor, CancellationToken.None);
        return testCases.Count;
    }

    private TestDataDescriptor CreateAsyncDescriptor()
    {
        // The rows are passed in rather than read from a field inside the iterator, so nothing here
        // depends on instance state that BenchmarkDotNet could reset between runs.
        var rows = _rows;

        return new TestDataDescriptor
        {
            BaseId = "NextUnit.Benchmarks.DataSourceExpansionBenchmarks.AddAsync",
            DisplayName = "AddAsync",
            TestClass = typeof(BenchmarkDataTarget),
            MethodName = nameof(BenchmarkDataTarget.Add),
            DataSourceName = "StreamedRows",
            DataSourceType = typeof(DataSourceExpansionBenchmarks),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            DeferredEnumeration = true,
            TestClassFactory = static (_, _) => new BenchmarkDataTarget(),
            TestMethodWithArguments = static (_, _, _) => Task.CompletedTask,
            AsyncDataSourceProvider = ct => StreamRowsAsync(rows, ct)
        };
    }

    /// <summary>
    /// Yields the same pre-built rows the synchronous source hands over, so the comparison measures
    /// the expander rather than the cost of fabricating rows.
    /// </summary>
    private static async IAsyncEnumerable<object?> StreamRowsAsync(
        object[][] rows,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    private TestDataDescriptor CreateDescriptor(bool deferred)
    {
        var rows = _rows;

        return new TestDataDescriptor
        {
            BaseId = "NextUnit.Benchmarks.DataSourceExpansionBenchmarks.Add",
            DisplayName = "Add",
            TestClass = typeof(BenchmarkDataTarget),
            MethodName = nameof(BenchmarkDataTarget.Add),
            DataSourceName = "Rows",
            DataSourceType = typeof(DataSourceExpansionBenchmarks),
            ParameterTypes = [typeof(int), typeof(int), typeof(int)],
            DeferredEnumeration = deferred,
            TestClassFactory = static (_, _) => new BenchmarkDataTarget(),
            TestMethodWithArguments = static (_, _, _) => Task.CompletedTask,
            DataSourceProvider = () => rows
        };
    }

    private sealed class BenchmarkDataTarget
    {
        public void Add(int a, int b, int expected)
        {
        }
    }
}
