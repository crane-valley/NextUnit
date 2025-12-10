using NextUnit;
using SpeedComparison.Shared;

namespace SpeedComparison.NextUnit;

/// <summary>
/// Simple tests - 50 tests with basic assertions
/// </summary>
public class SimpleTests
{
    [Test]
    public void Test01() => Assert.True(true);

    [Test]
    public void Test02() => Assert.False(false);

    [Test]
    public void Test03() => Assert.Equal(1, 1);

    [Test]
    public void Test04() => Assert.Equal("test", "test");

    [Test]
    public void Test05() => Assert.NotEqual(1, 2);

    [Test]
    public void Test06() => Assert.True(5 > 3);

    [Test]
    public void Test07() => Assert.False(2 > 5);

    [Test]
    public void Test08() => Assert.Equal(10, TestOperations.Add(5, 5));

    [Test]
    public void Test09() => Assert.Equal(0, TestOperations.Subtract(5, 5));

    [Test]
    public void Test10() => Assert.Equal(25, TestOperations.Multiply(5, 5));

    [Test]
    public void Test11() => Assert.True(true);

    [Test]
    public void Test12() => Assert.False(false);

    [Test]
    public void Test13() => Assert.Equal(2, 2);

    [Test]
    public void Test14() => Assert.Equal("hello", "hello");

    [Test]
    public void Test15() => Assert.NotEqual(3, 4);

    [Test]
    public void Test16() => Assert.True(10 > 5);

    [Test]
    public void Test17() => Assert.False(3 > 10);

    [Test]
    public void Test18() => Assert.Equal(8, TestOperations.Add(3, 5));

    [Test]
    public void Test19() => Assert.Equal(2, TestOperations.Subtract(5, 3));

    [Test]
    public void Test20() => Assert.Equal(15, TestOperations.Multiply(3, 5));

    [Test]
    public void Test21() => Assert.True(true);

    [Test]
    public void Test22() => Assert.False(false);

    [Test]
    public void Test23() => Assert.Equal(3, 3);

    [Test]
    public void Test24() => Assert.Equal("world", "world");

    [Test]
    public void Test25() => Assert.NotEqual(5, 6);

    [Test]
    public void Test26() => Assert.True(15 > 10);

    [Test]
    public void Test27() => Assert.False(5 > 15);

    [Test]
    public void Test28() => Assert.Equal(12, TestOperations.Add(7, 5));

    [Test]
    public void Test29() => Assert.Equal(2, TestOperations.Subtract(7, 5));

    [Test]
    public void Test30() => Assert.Equal(35, TestOperations.Multiply(7, 5));

    [Test]
    public void Test31() => Assert.True(true);

    [Test]
    public void Test32() => Assert.False(false);

    [Test]
    public void Test33() => Assert.Equal(4, 4);

    [Test]
    public void Test34() => Assert.Equal("test", "test");

    [Test]
    public void Test35() => Assert.NotEqual(7, 8);

    [Test]
    public void Test36() => Assert.True(20 > 15);

    [Test]
    public void Test37() => Assert.False(10 > 20);

    [Test]
    public void Test38() => Assert.Equal(15, TestOperations.Add(10, 5));

    [Test]
    public void Test39() => Assert.Equal(5, TestOperations.Subtract(10, 5));

    [Test]
    public void Test40() => Assert.Equal(50, TestOperations.Multiply(10, 5));

    [Test]
    public void Test41() => Assert.True(true);

    [Test]
    public void Test42() => Assert.False(false);

    [Test]
    public void Test43() => Assert.Equal(5, 5);

    [Test]
    public void Test44() => Assert.Equal("data", "data");

    [Test]
    public void Test45() => Assert.NotEqual(9, 10);

    [Test]
    public void Test46() => Assert.True(25 > 20);

    [Test]
    public void Test47() => Assert.False(15 > 25);

    [Test]
    public void Test48() => Assert.Equal(20, TestOperations.Add(15, 5));

    [Test]
    public void Test49() => Assert.Equal(10, TestOperations.Subtract(15, 5));

    [Test]
    public void Test50() => Assert.Equal(75, TestOperations.Multiply(15, 5));
}

/// <summary>
/// Parameterized tests - 50 tests (5 test methods x 10 parameter sets each)
/// </summary>
public class ParameterizedTests
{
    [Test]
    [Arguments(1, 2, 3)]
    [Arguments(5, 7, 12)]
    [Arguments(-1, 1, 0)]
    [Arguments(0, 0, 0)]
    [Arguments(100, 200, 300)]
    [Arguments(-50, -50, -100)]
    [Arguments(42, 58, 100)]
    [Arguments(999, 1, 1000)]
    [Arguments(-100, 100, 0)]
    [Arguments(7, 3, 10)]
    public void Addition_ReturnsCorrectSum(int a, int b, int expected)
    {
        var result = TestOperations.Add(a, b);
        Assert.Equal(expected, result);
    }

    [Test]
    [Arguments("hello", 5)]
    [Arguments("world", 5)]
    [Arguments("", 0)]
    [Arguments("a", 1)]
    [Arguments("test case", 9)]
    [Arguments("benchmark", 9)]
    [Arguments("NextUnit", 8)]
    [Arguments("x", 1)]
    [Arguments("testing framework", 17)]
    [Arguments("ab", 2)]
    public void StringLength_IsCorrect(string text, int expectedLength)
    {
        var result = TestOperations.GetLength(text);
        Assert.Equal(expectedLength, result);
    }

    [Test]
    [Arguments(5, 1, 10)]
    [Arguments(50, 0, 100)]
    [Arguments(0, -10, 10)]
    [Arguments(99, 90, 100)]
    [Arguments(-5, -10, 0)]
    [Arguments(25, 20, 30)]
    [Arguments(1, 1, 1)]
    [Arguments(75, 50, 100)]
    [Arguments(42, 40, 45)]
    [Arguments(10, 5, 15)]
    public void IsInRange_ReturnsTrue(int value, int min, int max)
    {
        var result = TestOperations.IsInRange(value, min, max);
        Assert.True(result);
    }

    [Test]
    [Arguments(2, 1, 1)]
    [Arguments(5, 3, 2)]
    [Arguments(10, 5, 5)]
    [Arguments(100, 50, 50)]
    [Arguments(0, 0, 0)]
    [Arguments(-5, -3, -2)]
    [Arguments(20, 10, 10)]
    [Arguments(7, 4, 3)]
    [Arguments(15, 8, 7)]
    [Arguments(30, 20, 10)]
    public void Subtraction_ReturnsCorrectResult(int a, int b, int expected)
    {
        var result = TestOperations.Subtract(a, b);
        Assert.Equal(expected, result);
    }

    [Test]
    [Arguments(2, 3, 6)]
    [Arguments(5, 5, 25)]
    [Arguments(10, 10, 100)]
    [Arguments(0, 100, 0)]
    [Arguments(1, 1, 1)]
    [Arguments(-2, 3, -6)]
    [Arguments(7, 8, 56)]
    [Arguments(9, 9, 81)]
    [Arguments(4, 5, 20)]
    [Arguments(6, 7, 42)]
    public void Multiplication_ReturnsCorrectResult(int a, int b, int expected)
    {
        var result = TestOperations.Multiply(a, b);
        Assert.Equal(expected, result);
    }
}

/// <summary>
/// Lifecycle tests - 25 tests with Before/After hooks
/// </summary>
public class LifecycleTests
{
    private int _setupCount;

    [Before(LifecycleScope.Test)]
    public void Setup()
    {
        TestOperations.PerformSetup();
        _setupCount++;
    }

    [After(LifecycleScope.Test)]
    public void Cleanup()
    {
        TestOperations.PerformCleanup();
    }

    [Test]
    public void LifecycleTest01() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest02() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest03() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest04() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest05() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest06() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest07() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest08() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest09() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest10() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest11() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest12() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest13() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest14() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest15() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest16() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest17() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest18() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest19() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest20() => Assert.True(_setupCount > 0);

    [Test]
    public void LifecycleTest21() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest22() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest23() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest24() => Assert.Equal(1, _setupCount);

    [Test]
    public void LifecycleTest25() => Assert.Equal(1, _setupCount);
}

/// <summary>
/// Async tests - 25 async test methods
/// </summary>
public class AsyncTests
{
    [Test]
    public async Task AsyncTest01()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.Equal(42, result);
    }

    [Test]
    public async Task AsyncTest02()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.True(result > 0);
    }

    [Test]
    public async Task AsyncTest03()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.Equal("result", result);
    }

    [Test]
    public async Task AsyncTest04()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.NotNull(result);
    }

    [Test]
    public async Task AsyncTest05()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.InRange(result, 0, 100);
    }

    [Test]
    public async Task AsyncTest06()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.Equal(42, result);
    }

    [Test]
    public async Task AsyncTest07()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.True(result > 0);
    }

    [Test]
    public async Task AsyncTest08()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.Equal("result", result);
    }

    [Test]
    public async Task AsyncTest09()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.NotNull(result);
    }

    [Test]
    public async Task AsyncTest10()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.InRange(result, 0, 100);
    }

    [Test]
    public async Task AsyncTest11()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.Equal(42, result);
    }

    [Test]
    public async Task AsyncTest12()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.True(result > 0);
    }

    [Test]
    public async Task AsyncTest13()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.Equal("result", result);
    }

    [Test]
    public async Task AsyncTest14()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.NotNull(result);
    }

    [Test]
    public async Task AsyncTest15()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.InRange(result, 0, 100);
    }

    [Test]
    public async Task AsyncTest16()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.Equal(42, result);
    }

    [Test]
    public async Task AsyncTest17()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.True(result > 0);
    }

    [Test]
    public async Task AsyncTest18()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.Equal("result", result);
    }

    [Test]
    public async Task AsyncTest19()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.NotNull(result);
    }

    [Test]
    public async Task AsyncTest20()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.InRange(result, 0, 100);
    }

    [Test]
    public async Task AsyncTest21()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.Equal(42, result);
    }

    [Test]
    public async Task AsyncTest22()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.True(result > 0);
    }

    [Test]
    public async Task AsyncTest23()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.Equal("result", result);
    }

    [Test]
    public async Task AsyncTest24()
    {
        var result = await TestOperations.GetStringAsync();
        Assert.NotNull(result);
    }

    [Test]
    public async Task AsyncTest25()
    {
        var result = await TestOperations.GetValueAsync();
        Assert.InRange(result, 0, 100);
    }
}

/// <summary>
/// Complex assertion tests - 25 tests with collection/string assertions
/// </summary>
public class ComplexAssertionTests
{
    [Test]
    public void Collection_Contains()
    {
        var numbers = SharedTestData.NumberCollection;
        Assert.Contains(5, numbers);
    }

    [Test]
    public void Collection_DoesNotContain()
    {
        var numbers = SharedTestData.NumberCollection;
        Assert.DoesNotContain(99, numbers);
    }

    [Test]
    public void Collection_NotEmpty()
    {
        var numbers = SharedTestData.NumberCollection;
        Assert.NotEmpty(numbers);
    }

    [Test]
    public void String_Contains()
    {
        Assert.Contains("ell", "hello");
    }

    [Test]
    public void String_StartsWith()
    {
        Assert.StartsWith("hel", "hello");
    }

    [Test]
    public void String_EndsWith()
    {
        Assert.EndsWith("llo", "hello");
    }

    [Test]
    public void Numeric_InRange()
    {
        Assert.InRange(5, 1, 10);
    }

    [Test]
    public void Numeric_NotInRange()
    {
        Assert.NotInRange(15, 1, 10);
    }

    [Test]
    public void Collection_All()
    {
        var numbers = SharedTestData.NumberCollection;
        Assert.All(numbers, n => Assert.True(n > 0));
    }

    [Test]
    public void StringCollection_Contains()
    {
        var strings = SharedTestData.StringCollection;
        Assert.Contains("apple", strings);
    }

    [Test]
    public void StringCollection_NotEmpty()
    {
        var strings = SharedTestData.StringCollection;
        Assert.NotEmpty(strings);
    }

    [Test]
    public void Collection_Contains2()
    {
        var numbers = SharedTestData.NumberCollection;
        Assert.Contains(1, numbers);
    }

    [Test]
    public void Collection_Contains3()
    {
        var numbers = SharedTestData.NumberCollection;
        Assert.Contains(10, numbers);
    }

    [Test]
    public void String_Contains2()
    {
        Assert.Contains("orl", "world");
    }

    [Test]
    public void String_StartsWith2()
    {
        Assert.StartsWith("wor", "world");
    }

    [Test]
    public void String_EndsWith2()
    {
        Assert.EndsWith("rld", "world");
    }

    [Test]
    public void Numeric_InRange2()
    {
        Assert.InRange(50, 0, 100);
    }

    [Test]
    public void Numeric_NotInRange2()
    {
        Assert.NotInRange(150, 0, 100);
    }

    [Test]
    public void Collection_All2()
    {
        var numbers = SharedTestData.NumberCollection;
        Assert.All(numbers, n => Assert.True(n < 100));
    }

    [Test]
    public void StringCollection_Contains2()
    {
        var strings = SharedTestData.StringCollection;
        Assert.Contains("banana", strings);
    }

    [Test]
    public void Collection_Contains4()
    {
        var numbers = SharedTestData.NumberCollection;
        Assert.Contains(7, numbers);
    }

    [Test]
    public void String_Contains3()
    {
        Assert.Contains("st", "test");
    }

    [Test]
    public void Numeric_InRange3()
    {
        Assert.InRange(75, 50, 100);
    }

    [Test]
    public void Collection_All3()
    {
        var numbers = SharedTestData.NumberCollection;
        Assert.All(numbers, n => Assert.InRange(n, 1, 10));
    }

    [Test]
    public void StringCollection_Contains3()
    {
        var strings = SharedTestData.StringCollection;
        Assert.Contains("cherry", strings);
    }
}

/// <summary>
/// Parallel tests - 25 tests that can run in parallel
/// </summary>
public class ParallelTests
{
    [Test]
    public void ParallelTest01() => Assert.Equal(2, TestOperations.Add(1, 1));

    [Test]
    public void ParallelTest02() => Assert.Equal(4, TestOperations.Add(2, 2));

    [Test]
    public void ParallelTest03() => Assert.Equal(6, TestOperations.Add(3, 3));

    [Test]
    public void ParallelTest04() => Assert.Equal(8, TestOperations.Add(4, 4));

    [Test]
    public void ParallelTest05() => Assert.Equal(10, TestOperations.Add(5, 5));

    [Test]
    public void ParallelTest06() => Assert.Equal(12, TestOperations.Add(6, 6));

    [Test]
    public void ParallelTest07() => Assert.Equal(14, TestOperations.Add(7, 7));

    [Test]
    public void ParallelTest08() => Assert.Equal(16, TestOperations.Add(8, 8));

    [Test]
    public void ParallelTest09() => Assert.Equal(18, TestOperations.Add(9, 9));

    [Test]
    public void ParallelTest10() => Assert.Equal(20, TestOperations.Add(10, 10));

    [Test]
    public void ParallelTest11() => Assert.Equal(0, TestOperations.Subtract(5, 5));

    [Test]
    public void ParallelTest12() => Assert.Equal(0, TestOperations.Subtract(10, 10));

    [Test]
    public void ParallelTest13() => Assert.Equal(0, TestOperations.Subtract(15, 15));

    [Test]
    public void ParallelTest14() => Assert.Equal(0, TestOperations.Subtract(20, 20));

    [Test]
    public void ParallelTest15() => Assert.Equal(0, TestOperations.Subtract(25, 25));

    [Test]
    public void ParallelTest16() => Assert.Equal(25, TestOperations.Multiply(5, 5));

    [Test]
    public void ParallelTest17() => Assert.Equal(36, TestOperations.Multiply(6, 6));

    [Test]
    public void ParallelTest18() => Assert.Equal(49, TestOperations.Multiply(7, 7));

    [Test]
    public void ParallelTest19() => Assert.Equal(64, TestOperations.Multiply(8, 8));

    [Test]
    public void ParallelTest20() => Assert.Equal(81, TestOperations.Multiply(9, 9));

    [Test]
    public void ParallelTest21() => Assert.True(TestOperations.IsInRange(5, 1, 10));

    [Test]
    public void ParallelTest22() => Assert.True(TestOperations.IsInRange(50, 0, 100));

    [Test]
    public void ParallelTest23() => Assert.True(TestOperations.IsInRange(25, 20, 30));

    [Test]
    public void ParallelTest24() => Assert.True(TestOperations.IsInRange(75, 50, 100));

    [Test]
    public void ParallelTest25() => Assert.True(TestOperations.IsInRange(10, 5, 15));
}
