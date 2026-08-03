#if NEXTUNIT_LOCAL_PACKAGE
using System.Runtime.CompilerServices;
#endif

namespace NextUnit.PackageSmoke;

public class BasicTests
{
    [NextUnit.Test]
    public void PackageRunsGeneratedTest()
    {
        NextUnit.Assert.Equal(4, 2 + 2);
    }

#if NEXTUNIT_LOCAL_PACKAGE
    public static IEnumerable<NextUnit.TestDataRow<(int A, int B, int Expected)>> Rows()
    {
        yield return new NextUnit.TestDataRow<(int A, int B, int Expected)>(
            (2, 3, 5),
            displayName: "package typed row",
            categories: ["PackageSmoke"],
            tags: ["TypedData"]);
    }

    [NextUnit.Test]
    [NextUnit.TestData(nameof(Rows))]
    public void PackageRunsTypedDataRow(int a, int b, int expected)
    {
        NextUnit.Assert.Equal(expected, a + b);
    }

    [NextUnit.Test]
    [NextUnit.TestData(nameof(Rows))]
    public ValueTask<int> PackageRunsValueTaskDataRow(int a, int b, int expected)
    {
        NextUnit.Assert.Equal(expected, a + b);
        return new ValueTask<int>(expected);
    }

    // Async member data has to survive trimming and Native AOT publishing, which is exactly what
    // this project verifies: the generated provider must bind the source statically, with no
    // runtime reflection to be trimmed away.
    public static async IAsyncEnumerable<object[]> StreamedRowsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return [10, 20, 30];
        yield return [1, 1, 2];
    }

    [NextUnit.Test]
    [NextUnit.TestData(nameof(StreamedRowsAsync))]
    public void PackageRunsAsyncEnumerableDataRow(int a, int b, int expected)
    {
        NextUnit.Assert.Equal(expected, a + b);
    }

    public static Task<IEnumerable<object[]>> TaskRows() =>
        Task.FromResult<IEnumerable<object[]>>([[4, 5, 9]]);

    [NextUnit.Test]
    [NextUnit.TestData(nameof(TaskRows))]
    public void PackageRunsTaskWrappedDataRow(int a, int b, int expected)
    {
        NextUnit.Assert.Equal(expected, a + b);
    }
#endif
}
