using NUnit.Framework;
using SpeedComparison.Shared;

namespace SpeedComparison.NUnit;

/// <summary>
/// Simple tests - 50 tests with basic assertions
/// </summary>
[TestFixture]
public class SimpleTests
{
    [Test] public void Test01() => Assert.That(true, Is.True);
    [Test] public void Test02() => Assert.That(false, Is.False);
    [Test] public void Test03() => Assert.That(1, Is.EqualTo(1));
    [Test] public void Test04() => Assert.That("test", Is.EqualTo("test"));
    [Test] public void Test05() => Assert.That(1, Is.Not.EqualTo(2));
    [Test] public void Test06() => Assert.That(5 > 3, Is.True);
    [Test] public void Test07() => Assert.That(2 > 5, Is.False);
    [Test] public void Test08() => Assert.That(TestOperations.Add(5, 5), Is.EqualTo(10));
    [Test] public void Test09() => Assert.That(TestOperations.Subtract(5, 5), Is.EqualTo(0));
    [Test] public void Test10() => Assert.That(TestOperations.Multiply(5, 5), Is.EqualTo(25));
    [Test] public void Test11() => Assert.That(true, Is.True);
    [Test] public void Test12() => Assert.That(false, Is.False);
    [Test] public void Test13() => Assert.That(2, Is.EqualTo(2));
    [Test] public void Test14() => Assert.That("hello", Is.EqualTo("hello"));
    [Test] public void Test15() => Assert.That(3, Is.Not.EqualTo(4));
    [Test] public void Test16() => Assert.That(10 > 5, Is.True);
    [Test] public void Test17() => Assert.That(3 > 10, Is.False);
    [Test] public void Test18() => Assert.That(TestOperations.Add(3, 5), Is.EqualTo(8));
    [Test] public void Test19() => Assert.That(TestOperations.Subtract(5, 3), Is.EqualTo(2));
    [Test] public void Test20() => Assert.That(TestOperations.Multiply(3, 5), Is.EqualTo(15));
    [Test] public void Test21() => Assert.That(true, Is.True);
    [Test] public void Test22() => Assert.That(false, Is.False);
    [Test] public void Test23() => Assert.That(3, Is.EqualTo(3));
    [Test] public void Test24() => Assert.That("world", Is.EqualTo("world"));
    [Test] public void Test25() => Assert.That(5, Is.Not.EqualTo(6));
    [Test] public void Test26() => Assert.That(15 > 10, Is.True);
    [Test] public void Test27() => Assert.That(5 > 15, Is.False);
    [Test] public void Test28() => Assert.That(TestOperations.Add(7, 5), Is.EqualTo(12));
    [Test] public void Test29() => Assert.That(TestOperations.Subtract(7, 5), Is.EqualTo(2));
    [Test] public void Test30() => Assert.That(TestOperations.Multiply(7, 5), Is.EqualTo(35));
    [Test] public void Test31() => Assert.That(true, Is.True);
    [Test] public void Test32() => Assert.That(false, Is.False);
    [Test] public void Test33() => Assert.That(4, Is.EqualTo(4));
    [Test] public void Test34() => Assert.That("test", Is.EqualTo("test"));
    [Test] public void Test35() => Assert.That(7, Is.Not.EqualTo(8));
    [Test] public void Test36() => Assert.That(20 > 15, Is.True);
    [Test] public void Test37() => Assert.That(10 > 20, Is.False);
    [Test] public void Test38() => Assert.That(TestOperations.Add(10, 5), Is.EqualTo(15));
    [Test] public void Test39() => Assert.That(TestOperations.Subtract(10, 5), Is.EqualTo(5));
    [Test] public void Test40() => Assert.That(TestOperations.Multiply(10, 5), Is.EqualTo(50));
    [Test] public void Test41() => Assert.That(true, Is.True);
    [Test] public void Test42() => Assert.That(false, Is.False);
    [Test] public void Test43() => Assert.That(5, Is.EqualTo(5));
    [Test] public void Test44() => Assert.That("data", Is.EqualTo("data"));
    [Test] public void Test45() => Assert.That(9, Is.Not.EqualTo(10));
    [Test] public void Test46() => Assert.That(25 > 20, Is.True);
    [Test] public void Test47() => Assert.That(15 > 25, Is.False);
    [Test] public void Test48() => Assert.That(TestOperations.Add(15, 5), Is.EqualTo(20));
    [Test] public void Test49() => Assert.That(TestOperations.Subtract(15, 5), Is.EqualTo(10));
    [Test] public void Test50() => Assert.That(TestOperations.Multiply(15, 5), Is.EqualTo(75));
}

[TestFixture]
public class ParameterizedTests
{
    private static IEnumerable<TestCaseData> AdditionTestCases()
    {
        foreach (var tc in SharedTestData.AdditionTestCases())
            yield return new TestCaseData(tc[0], tc[1], tc[2]);
    }

    [Test, TestCaseSource(nameof(AdditionTestCases))]
    public void Addition_ReturnsCorrectSum(int a, int b, int expected) => Assert.That(TestOperations.Add(a, b), Is.EqualTo(expected));

    private static IEnumerable<TestCaseData> StringLengthTestCases()
    {
        foreach (var tc in SharedTestData.StringLengthTestCases())
            yield return new TestCaseData(tc[0], tc[1]);
    }

    [Test, TestCaseSource(nameof(StringLengthTestCases))]
    public void StringLength_IsCorrect(string text, int expectedLength) => Assert.That(TestOperations.GetLength(text), Is.EqualTo(expectedLength));

    private static IEnumerable<TestCaseData> RangeTestCases()
    {
        foreach (var tc in SharedTestData.RangeTestCases())
            yield return new TestCaseData(tc[0], tc[1], tc[2]);
    }

    [Test, TestCaseSource(nameof(RangeTestCases))]
    public void IsInRange_ReturnsTrue(int value, int min, int max) => Assert.That(TestOperations.IsInRange(value, min, max), Is.True);

    [Test, TestCase(2, 1, 1), TestCase(5, 3, 2), TestCase(10, 5, 5), TestCase(100, 50, 50), TestCase(0, 0, 0), TestCase(-5, -3, -2), TestCase(20, 10, 10), TestCase(7, 4, 3), TestCase(15, 8, 7), TestCase(30, 20, 10)]
    public void Subtraction_ReturnsCorrectResult(int a, int b, int expected) => Assert.That(TestOperations.Subtract(a, b), Is.EqualTo(expected));

    [Test, TestCase(2, 3, 6), TestCase(5, 5, 25), TestCase(10, 10, 100), TestCase(0, 100, 0), TestCase(1, 1, 1), TestCase(-2, 3, -6), TestCase(7, 8, 56), TestCase(9, 9, 81), TestCase(4, 5, 20), TestCase(6, 7, 42)]
    public void Multiplication_ReturnsCorrectResult(int a, int b, int expected) => Assert.That(TestOperations.Multiply(a, b), Is.EqualTo(expected));
}

[TestFixture]
public class LifecycleTests
{
    private int _setupCount;

    [SetUp]
    public void Setup()
    {
        TestOperations.PerformSetup();
        _setupCount = 1;
    }

    [TearDown]
    public void Cleanup() => TestOperations.PerformCleanup();

    [Test] public void LifecycleTest01() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest02() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest03() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest04() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest05() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest06() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest07() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest08() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest09() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest10() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest11() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest12() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest13() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest14() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest15() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest16() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest17() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest18() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest19() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest20() => Assert.That(_setupCount > 0, Is.True);
    [Test] public void LifecycleTest21() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest22() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest23() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest24() => Assert.That(_setupCount, Is.EqualTo(1));
    [Test] public void LifecycleTest25() => Assert.That(_setupCount, Is.EqualTo(1));
}

[TestFixture]
public class AsyncTests
{
    [Test] public async Task AsyncTest01() => Assert.That(await TestOperations.GetValueAsync(), Is.EqualTo(42));
    [Test] public async Task AsyncTest02() => Assert.That(await TestOperations.GetValueAsync() > 0, Is.True);
    [Test] public async Task AsyncTest03() => Assert.That(await TestOperations.GetStringAsync(), Is.EqualTo("result"));
    [Test] public async Task AsyncTest04() => Assert.That(await TestOperations.GetStringAsync(), Is.Not.Null);
    [Test] public async Task AsyncTest05() => Assert.That(await TestOperations.GetValueAsync(), Is.InRange(0, 100));
    [Test] public async Task AsyncTest06() => Assert.That(await TestOperations.GetValueAsync(), Is.EqualTo(42));
    [Test] public async Task AsyncTest07() => Assert.That(await TestOperations.GetValueAsync() > 0, Is.True);
    [Test] public async Task AsyncTest08() => Assert.That(await TestOperations.GetStringAsync(), Is.EqualTo("result"));
    [Test] public async Task AsyncTest09() => Assert.That(await TestOperations.GetStringAsync(), Is.Not.Null);
    [Test] public async Task AsyncTest10() => Assert.That(await TestOperations.GetValueAsync(), Is.InRange(0, 100));
    [Test] public async Task AsyncTest11() => Assert.That(await TestOperations.GetValueAsync(), Is.EqualTo(42));
    [Test] public async Task AsyncTest12() => Assert.That(await TestOperations.GetValueAsync() > 0, Is.True);
    [Test] public async Task AsyncTest13() => Assert.That(await TestOperations.GetStringAsync(), Is.EqualTo("result"));
    [Test] public async Task AsyncTest14() => Assert.That(await TestOperations.GetStringAsync(), Is.Not.Null);
    [Test] public async Task AsyncTest15() => Assert.That(await TestOperations.GetValueAsync(), Is.InRange(0, 100));
    [Test] public async Task AsyncTest16() => Assert.That(await TestOperations.GetValueAsync(), Is.EqualTo(42));
    [Test] public async Task AsyncTest17() => Assert.That(await TestOperations.GetValueAsync() > 0, Is.True);
    [Test] public async Task AsyncTest18() => Assert.That(await TestOperations.GetStringAsync(), Is.EqualTo("result"));
    [Test] public async Task AsyncTest19() => Assert.That(await TestOperations.GetStringAsync(), Is.Not.Null);
    [Test] public async Task AsyncTest20() => Assert.That(await TestOperations.GetValueAsync(), Is.InRange(0, 100));
    [Test] public async Task AsyncTest21() => Assert.That(await TestOperations.GetValueAsync(), Is.EqualTo(42));
    [Test] public async Task AsyncTest22() => Assert.That(await TestOperations.GetValueAsync() > 0, Is.True);
    [Test] public async Task AsyncTest23() => Assert.That(await TestOperations.GetStringAsync(), Is.EqualTo("result"));
    [Test] public async Task AsyncTest24() => Assert.That(await TestOperations.GetStringAsync(), Is.Not.Null);
    [Test] public async Task AsyncTest25() => Assert.That(await TestOperations.GetValueAsync(), Is.InRange(0, 100));
}

[TestFixture]
public class ComplexAssertionTests
{
    [Test] public void Collection_Contains() => Assert.That(SharedTestData.NumberCollection, Does.Contain(5));
    [Test] public void Collection_DoesNotContain() => Assert.That(SharedTestData.NumberCollection, Does.Not.Contain(99));
    [Test] public void Collection_NotEmpty() => Assert.That(SharedTestData.NumberCollection, Is.Not.Empty);
    [Test] public void String_Contains() => Assert.That("hello", Does.Contain("ell"));
    [Test] public void String_StartsWith() => Assert.That("hello", Does.StartWith("hel"));
    [Test] public void String_EndsWith() => Assert.That("hello", Does.EndWith("llo"));
    [Test] public void Numeric_InRange() => Assert.That(5, Is.InRange(1, 10));
    [Test] public void Numeric_NotInRange() => Assert.That(15, Is.Not.InRange(1, 10));
    [Test] public void Collection_All() => Assert.That(SharedTestData.NumberCollection, Has.All.GreaterThan(0));
    [Test] public void StringCollection_Contains() => Assert.That(SharedTestData.StringCollection, Does.Contain("apple"));
    [Test] public void StringCollection_NotEmpty() => Assert.That(SharedTestData.StringCollection, Is.Not.Empty);
    [Test] public void Collection_Contains2() => Assert.That(SharedTestData.NumberCollection, Does.Contain(1));
    [Test] public void Collection_Contains3() => Assert.That(SharedTestData.NumberCollection, Does.Contain(10));
    [Test] public void String_Contains2() => Assert.That("world", Does.Contain("orl"));
    [Test] public void String_StartsWith2() => Assert.That("world", Does.StartWith("wor"));
    [Test] public void String_EndsWith2() => Assert.That("world", Does.EndWith("rld"));
    [Test] public void Numeric_InRange2() => Assert.That(50, Is.InRange(0, 100));
    [Test] public void Numeric_NotInRange2() => Assert.That(150, Is.Not.InRange(0, 100));
    [Test] public void Collection_All2() => Assert.That(SharedTestData.NumberCollection, Has.All.LessThan(100));
    [Test] public void StringCollection_Contains2() => Assert.That(SharedTestData.StringCollection, Does.Contain("banana"));
    [Test] public void Collection_Contains4() => Assert.That(SharedTestData.NumberCollection, Does.Contain(7));
    [Test] public void String_Contains3() => Assert.That("test", Does.Contain("st"));
    [Test] public void Numeric_InRange3() => Assert.That(75, Is.InRange(50, 100));
    [Test] public void Collection_All3() => Assert.That(SharedTestData.NumberCollection, Has.All.InRange(1, 10));
    [Test] public void StringCollection_Contains3() => Assert.That(SharedTestData.StringCollection, Does.Contain("cherry"));
}

[TestFixture, Parallelizable(ParallelScope.All)]
public class ParallelTests
{
    [Test] public void ParallelTest01() => Assert.That(TestOperations.Add(1, 1), Is.EqualTo(2));
    [Test] public void ParallelTest02() => Assert.That(TestOperations.Add(2, 2), Is.EqualTo(4));
    [Test] public void ParallelTest03() => Assert.That(TestOperations.Add(3, 3), Is.EqualTo(6));
    [Test] public void ParallelTest04() => Assert.That(TestOperations.Add(4, 4), Is.EqualTo(8));
    [Test] public void ParallelTest05() => Assert.That(TestOperations.Add(5, 5), Is.EqualTo(10));
    [Test] public void ParallelTest06() => Assert.That(TestOperations.Add(6, 6), Is.EqualTo(12));
    [Test] public void ParallelTest07() => Assert.That(TestOperations.Add(7, 7), Is.EqualTo(14));
    [Test] public void ParallelTest08() => Assert.That(TestOperations.Add(8, 8), Is.EqualTo(16));
    [Test] public void ParallelTest09() => Assert.That(TestOperations.Add(9, 9), Is.EqualTo(18));
    [Test] public void ParallelTest10() => Assert.That(TestOperations.Add(10, 10), Is.EqualTo(20));
    [Test] public void ParallelTest11() => Assert.That(TestOperations.Subtract(5, 5), Is.EqualTo(0));
    [Test] public void ParallelTest12() => Assert.That(TestOperations.Subtract(10, 10), Is.EqualTo(0));
    [Test] public void ParallelTest13() => Assert.That(TestOperations.Subtract(15, 15), Is.EqualTo(0));
    [Test] public void ParallelTest14() => Assert.That(TestOperations.Subtract(20, 20), Is.EqualTo(0));
    [Test] public void ParallelTest15() => Assert.That(TestOperations.Subtract(25, 25), Is.EqualTo(0));
    [Test] public void ParallelTest16() => Assert.That(TestOperations.Multiply(5, 5), Is.EqualTo(25));
    [Test] public void ParallelTest17() => Assert.That(TestOperations.Multiply(6, 6), Is.EqualTo(36));
    [Test] public void ParallelTest18() => Assert.That(TestOperations.Multiply(7, 7), Is.EqualTo(49));
    [Test] public void ParallelTest19() => Assert.That(TestOperations.Multiply(8, 8), Is.EqualTo(64));
    [Test] public void ParallelTest20() => Assert.That(TestOperations.Multiply(9, 9), Is.EqualTo(81));
    [Test] public void ParallelTest21() => Assert.That(TestOperations.IsInRange(5, 1, 10), Is.True);
    [Test] public void ParallelTest22() => Assert.That(TestOperations.IsInRange(50, 0, 100), Is.True);
    [Test] public void ParallelTest23() => Assert.That(TestOperations.IsInRange(25, 20, 30), Is.True);
    [Test] public void ParallelTest24() => Assert.That(TestOperations.IsInRange(75, 50, 100), Is.True);
    [Test] public void ParallelTest25() => Assert.That(TestOperations.IsInRange(10, 5, 15), Is.True);
}
