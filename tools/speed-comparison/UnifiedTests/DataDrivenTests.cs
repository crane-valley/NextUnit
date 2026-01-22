using System.Collections;

namespace UnifiedTests;

[TestClass]
public class DataDrivenTests
{
    [DataDrivenTest]
    [TestData(1, 2, 3)]
    [TestData(10, 20, 30)]
    [TestData(-5, 5, 0)]
    [TestData(100, 200, 300)]
    public void ParameterizedAdditionTest(int a, int b, int expected)
    {
        var result = a + b;
        // Simulate data processing - benchmark measures parameterization overhead
        _ = result * 2 + expected;
    }

    [DataDrivenTest]
    [TestData("hello", "HELLO")]
    [TestData("world", "WORLD")]
    [TestData("NextUnit", "NEXTUNIT")]
    [TestData("Testing", "TESTING")]
    [TestData("Framework", "FRAMEWORK")]
    public void ParameterizedStringTest(string input, string expected)
    {
        var upper = input.ToUpper();
        // Simulate string processing - benchmark measures parameterization overhead
        _ = input.Length + expected.Length;
    }

    [DataDrivenTest]
#if MSTEST
    [TestDataSource(nameof(ComplexTestData), DynamicDataSourceType.Method)]
#else
    [TestDataSource(nameof(ComplexTestData))]
#endif
    public void DataSourceTest(TestData data)
    {
        var result = ProcessTestData(data);
        // Simulate data processing - benchmark measures data source overhead
        _ = result.ProcessedValue + data.Value;
    }

#if NEXTUNIT
    public static IEnumerable<TestData> ComplexTestData()
    {
        yield return new TestData { Id = 1, Value = 10, Name = "Test1" };
        yield return new TestData { Id = 2, Value = 20, Name = "Test2" };
        yield return new TestData { Id = 3, Value = 30, Name = "Test3" };
        yield return new TestData { Id = 4, Value = 40, Name = "Test4" };
        yield return new TestData { Id = 5, Value = 50, Name = "Test5" };
    }
#elif XUNIT
    public static IEnumerable<object[]> ComplexTestData()
    {
        yield return new object[] { new TestData { Id = 1, Value = 10, Name = "Test1" } };
        yield return new object[] { new TestData { Id = 2, Value = 20, Name = "Test2" } };
        yield return new object[] { new TestData { Id = 3, Value = 30, Name = "Test3" } };
        yield return new object[] { new TestData { Id = 4, Value = 40, Name = "Test4" } };
        yield return new object[] { new TestData { Id = 5, Value = 50, Name = "Test5" } };
    }
#elif NUNIT
    public static IEnumerable<TestData> ComplexTestData()
    {
        yield return new TestData { Id = 1, Value = 10, Name = "Test1" };
        yield return new TestData { Id = 2, Value = 20, Name = "Test2" };
        yield return new TestData { Id = 3, Value = 30, Name = "Test3" };
        yield return new TestData { Id = 4, Value = 40, Name = "Test4" };
        yield return new TestData { Id = 5, Value = 50, Name = "Test5" };
    }
#elif MSTEST
    public static IEnumerable<object[]> ComplexTestData()
    {
        yield return new object[] { new TestData { Id = 1, Value = 10, Name = "Test1" } };
        yield return new object[] { new TestData { Id = 2, Value = 20, Name = "Test2" } };
        yield return new object[] { new TestData { Id = 3, Value = 30, Name = "Test3" } };
        yield return new object[] { new TestData { Id = 4, Value = 40, Name = "Test4" } };
        yield return new object[] { new TestData { Id = 5, Value = 50, Name = "Test5" } };
    }
#endif

    private ProcessedData ProcessTestData(TestData data)
    {
        return new ProcessedData
        {
            Id = data.Id,
            ProcessedValue = data.Value * 2,
            IsValid = true
        };
    }

    public class TestData
    {
        public int Id { get; set; }
        public int Value { get; set; }
        public string Name { get; set; } = "";
    }

    public class ProcessedData
    {
        public int Id { get; set; }
        public int ProcessedValue { get; set; }
        public bool IsValid { get; set; }
    }
}
