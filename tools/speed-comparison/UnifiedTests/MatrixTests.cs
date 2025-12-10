namespace UnifiedTests;

[TestClass]
public class MatrixTests
{
    // Matrix tests - multi-dimensional parameterized tests
    [DataDrivenTest]
    [TestData(1, "A", true)]
    [TestData(1, "A", false)]
    [TestData(1, "B", true)]
    [TestData(1, "B", false)]
    [TestData(2, "A", true)]
    [TestData(2, "A", false)]
    [TestData(2, "B", true)]
    [TestData(2, "B", false)]
    [TestData(3, "A", true)]
    [TestData(3, "A", false)]
    [TestData(3, "B", true)]
    [TestData(3, "B", false)]
    public void MatrixTest_3x2x2(int number, string letter, bool flag)
    {
        var result = ProcessMatrix(number, letter, flag);
        var valid = result.Length > 0;
    }

    [DataDrivenTest]
    [TestData(1, 1, 1)]
    [TestData(1, 1, 2)]
    [TestData(1, 2, 1)]
    [TestData(1, 2, 2)]
    [TestData(2, 1, 1)]
    [TestData(2, 1, 2)]
    [TestData(2, 2, 1)]
    [TestData(2, 2, 2)]
    public void MatrixTest_2x2x2_Numeric(int x, int y, int z)
    {
        var result = x * y * z;
        var sum = x + y + z;
        var valid = result >= 0;
    }

    [DataDrivenTest]
    [TestData("A", "X", 1)]
    [TestData("A", "X", 2)]
    [TestData("A", "Y", 1)]
    [TestData("A", "Y", 2)]
    [TestData("B", "X", 1)]
    [TestData("B", "X", 2)]
    [TestData("B", "Y", 1)]
    [TestData("B", "Y", 2)]
    [TestData("C", "X", 1)]
    [TestData("C", "X", 2)]
    [TestData("C", "Y", 1)]
    [TestData("C", "Y", 2)]
    public void MatrixTest_3x2x2_String(string category, string type, int value)
    {
        var result = $"{category}_{type}_{value}";
        var length = result.Length;
        var valid = length > 0;
    }

    private string ProcessMatrix(int number, string letter, bool flag)
    {
        return $"{number}_{letter}_{flag}";
    }
}
