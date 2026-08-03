using System.Runtime.CompilerServices;

namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating asynchronous [TestData] member sources.
/// </summary>
/// <remarks>
/// Rows are enumerated once during discovery, so each row is an individually selectable and
/// filterable test case, exactly as it is for a synchronous source. This file is also the Native
/// AOT check for the feature: the sample project is what the AOT workflow publishes.
/// </remarks>
public class AsyncDataSourceTests
{
    /// <summary>
    /// A cancellation-aware asynchronous source. The token is the discovery cancellation token, so
    /// a slow source can be interrupted instead of stalling the run.
    /// </summary>
    public static async IAsyncEnumerable<object[]> StreamedAdditionRowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var row in new[] { new object[] { 2, 3, 5 }, new object[] { 10, 20, 30 } })
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    [Test]
    [TestData(nameof(StreamedAdditionRowsAsync))]
    public void Add_FromAsyncEnumerable(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }

    /// <summary>
    /// A source that fetches its rows once and returns them as a task-wrapped collection.
    /// </summary>
    public static async Task<IEnumerable<object[]>> LoadMultiplicationRowsAsync()
    {
        await Task.Yield();
        return [[2, 3, 6], [4, 5, 20]];
    }

    [Test]
    [TestData(nameof(LoadMultiplicationRowsAsync))]
    public void Multiply_FromTaskWrappedCollection(int a, int b, int expected)
    {
        Assert.Equal(expected, a * b);
    }

    /// <summary>
    /// Typed rows keep their per-row metadata when they arrive from an asynchronous source.
    /// </summary>
    public static async IAsyncEnumerable<TestDataRow<(int Value, bool IsEven)>> StreamedTypedRowsAsync()
    {
        await Task.Yield();
        yield return new TestDataRow<(int Value, bool IsEven)>(
            (4, true),
            displayName: "four is even",
            categories: ["AsyncData"]);
        yield return new TestDataRow<(int Value, bool IsEven)>(
            (7, false),
            displayName: "seven is odd",
            tags: ["Streamed"]);
    }

    [Test]
    [TestData(nameof(StreamedTypedRowsAsync))]
    public void Parity_FromAsyncTypedRows(int value, bool isEven)
    {
        Assert.Equal(isEven, value % 2 == 0);
    }

    /// <summary>
    /// A value-task-wrapped collection, the cheapest shape for rows that are already in memory.
    /// </summary>
    public static ValueTask<IReadOnlyList<object[]>> SubtractionRows() =>
        new(new object[][] { [9, 4, 5] });

    [Test]
    [TestData(nameof(SubtractionRows))]
    public void Subtract_FromValueTaskWrappedCollection(int a, int b, int expected)
    {
        Assert.Equal(expected, a - b);
    }
}
