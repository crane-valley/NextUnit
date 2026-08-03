using System.Runtime.CompilerServices;

namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating deferred [TestData] member sources.
/// </summary>
/// <remarks>
/// Discovery reports one placeholder per deferred source and the rows become individual test results
/// only once the run reaches them. This file is also the Native AOT check for the feature: the
/// sample project is what the AOT workflow publishes, so the generated provider has to survive
/// trimming even though it is invoked from the execution engine rather than from discovery.
/// </remarks>
public class DeferredDataSourceTests
{
    /// <summary>
    /// A source that would be expensive to enumerate at startup. Deferring it keeps discovery
    /// constant-time no matter how many rows the source can produce.
    /// </summary>
    public static IEnumerable<object[]> WideAdditionRows()
    {
        for (var i = 0; i < 4; i++)
        {
            yield return [i, i, i * 2];
        }
    }

    [Test]
    [TestData(nameof(WideAdditionRows), DeferredEnumeration = true)]
    public void Add_FromDeferredSource(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }

    /// <summary>
    /// Deferral and row shape are independent: an asynchronous source enumerates during execution
    /// with the run cancellation token instead of the discovery one.
    /// </summary>
    public static async IAsyncEnumerable<object[]> StreamedDeferredRowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var row in new[] { new object[] { 6, 2, 3 }, new object[] { 9, 3, 3 } })
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    [Test]
    [TestData(nameof(StreamedDeferredRowsAsync), DeferredEnumeration = true)]
    public void Divide_FromDeferredAsyncSource(int a, int b, int expected)
    {
        Assert.Equal(expected, a / b);
    }
}
