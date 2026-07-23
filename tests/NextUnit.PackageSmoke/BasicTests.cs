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
#endif
}
