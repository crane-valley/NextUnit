namespace NextUnit.SampleTests;

/// <summary>
/// Tests demonstrating the ClassDataSource attribute for class-based test data.
/// </summary>
public class ClassDataSourceTests
{
    #region Data Source Classes

    /// <summary>
    /// Simple data source providing multiplication test cases.
    /// </summary>
    public class MultiplicationTestData : IEnumerable<object?[]>
    {
        public IEnumerator<object?[]> GetEnumerator()
        {
            yield return [2, 3, 6];
            yield return [4, 5, 20];
            yield return [-1, 3, -3];
            yield return [0, 100, 0];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Data source providing addition test cases.
    /// </summary>
    public class AdditionTestData : IEnumerable<object?[]>
    {
        public IEnumerator<object?[]> GetEnumerator()
        {
            yield return [1, 1, 2];
            yield return [10, 20, 30];
            yield return [-5, 5, 0];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Data source providing subtraction test cases.
    /// </summary>
    public class SubtractionTestData : IEnumerable<object?[]>
    {
        public IEnumerator<object?[]> GetEnumerator()
        {
            yield return [5, 3, 2];
            yield return [10, 10, 0];
            yield return [0, 5, -5];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Data source for string validation tests.
    /// </summary>
    public class StringValidationTestData : IEnumerable<object?[]>
    {
        public IEnumerator<object?[]> GetEnumerator()
        {
            yield return ["hello", true];
            yield return ["", false];
            yield return [null, false];
            yield return ["  ", false];
            yield return ["valid string", true];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Shared data source that tracks instance creation for testing.
    /// Thread-safe using Interlocked.Increment for parallel test execution.
    /// </summary>
    public class SharedCountingTestData : IEnumerable<object?[]>
    {
        private static int _instanceCount;

        public SharedCountingTestData()
        {
            InstanceId = System.Threading.Interlocked.Increment(ref _instanceCount);
        }

        public int InstanceId { get; }

        public static void ResetCounter() => System.Threading.Interlocked.Exchange(ref _instanceCount, 0);

        public IEnumerator<object?[]> GetEnumerator()
        {
            yield return [InstanceId, 1];
            yield return [InstanceId, 2];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #endregion

    #region Basic ClassDataSource Tests

    [Test]
    [ClassDataSource<MultiplicationTestData>]
    public void Multiply_ReturnsCorrectProduct(int a, int b, int expected)
    {
        var result = a * b;
        Assert.Equal(expected, result);
    }

    [Test]
    [ClassDataSource<AdditionTestData>]
    public void Add_ReturnsCorrectSum(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }

    [Test]
    [ClassDataSource<StringValidationTestData>]
    public void IsValidString_ReturnsCorrectResult(string? input, bool expected)
    {
        var result = !string.IsNullOrWhiteSpace(input);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Multi-Type ClassDataSource Tests

    [Test]
    [ClassDataSource<AdditionTestData, SubtractionTestData>]
    public void MathOperations_FromMultipleDataSources(int a, int b, int expected)
    {
        // This test receives data from both AdditionTestData and SubtractionTestData
        // The first three rows are addition tests, the last three are subtraction tests
        // We verify that (a + b == expected) OR (a - b == expected)
        var addResult = a + b;
        var subResult = a - b;
        Assert.True(addResult == expected || subResult == expected,
            $"Expected {expected} from either {a}+{b}={addResult} or {a}-{b}={subResult}");
    }

    #endregion

    #region Shared Instance Tests

    [Test]
    [ClassDataSource<SharedCountingTestData>(Shared = SharedType.PerClass)]
    public void SharedPerClass_Test1(int instanceId, int dataIndex)
    {
        // All test cases in this class with the same SharedType.PerClass should use the same instance
        Assert.True(instanceId > 0, "Instance ID should be positive");
        Assert.True(dataIndex > 0, "Data index should be positive");
    }

    [Test]
    [ClassDataSource<SharedCountingTestData>(Shared = SharedType.PerClass)]
    public void SharedPerClass_Test2(int instanceId, int dataIndex)
    {
        // This test should use the same SharedCountingTestData instance as SharedPerClass_Test1
        Assert.True(instanceId > 0, "Instance ID should be positive");
        Assert.True(dataIndex > 0, "Data index should be positive");
    }

    [Test]
    [ClassDataSource<SharedCountingTestData>(Shared = SharedType.Keyed, Key = "group-a")]
    public void SharedKeyed_GroupA_Test1(int instanceId, int dataIndex)
    {
        // Tests with the same key share an instance
        Assert.True(instanceId > 0);
    }

    [Test]
    [ClassDataSource<SharedCountingTestData>(Shared = SharedType.Keyed, Key = "group-a")]
    public void SharedKeyed_GroupA_Test2(int instanceId, int dataIndex)
    {
        // Same key as GroupA_Test1, should share instance
        Assert.True(instanceId > 0);
    }

    [Test]
    [ClassDataSource<SharedCountingTestData>(Shared = SharedType.Keyed, Key = "group-b")]
    public void SharedKeyed_GroupB_Test(int instanceId, int dataIndex)
    {
        // Different key, different instance
        Assert.True(instanceId > 0);
    }

    #endregion

    #region Integration with Other Features

    [Test]
    [DisplayName("Parameterized multiplication: {0} × {1} = {2}")]
    [ClassDataSource<MultiplicationTestData>]
    public void DisplayName_WithClassDataSource(int a, int b, int expected)
    {
        Assert.Equal(expected, a * b);
    }

    [Test]
    [Category("Math")]
    [Tag("ClassData")]
    [ClassDataSource<AdditionTestData>]
    public void CategoryAndTag_WithClassDataSource(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }

    #endregion
}

/// <summary>
/// Additional test class to verify PerAssembly sharing works across classes.
/// </summary>
public class ClassDataSourceTests2
{
    /// <summary>
    /// Data source for PerAssembly sharing tests.
    /// Thread-safe using Interlocked.Increment for parallel test execution.
    /// </summary>
    public class AssemblySharedTestData : IEnumerable<object?[]>
    {
        private static int _globalInstanceCount;
        public int InstanceId { get; }

        public AssemblySharedTestData()
        {
            InstanceId = System.Threading.Interlocked.Increment(ref _globalInstanceCount);
        }

        public IEnumerator<object?[]> GetEnumerator()
        {
            yield return [InstanceId];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Test]
    [ClassDataSource<AssemblySharedTestData>(Shared = SharedType.PerAssembly)]
    public void PerAssembly_AcrossClasses(int instanceId)
    {
        // This should share instance with tests in other classes using PerAssembly
        Assert.True(instanceId > 0);
    }
}
