using Microsoft.VisualStudio.TestTools.UnitTesting;
using SpeedComparison.Shared;

namespace SpeedComparison.MSTest;

[TestClass]
public class SimpleTests
{
    [TestMethod] public void Test01() => Assert.IsTrue(true);
    [TestMethod] public void Test02() => Assert.IsFalse(false);
    [TestMethod] public void Test03() => Assert.AreEqual(1, 1);
    [TestMethod] public void Test04() => Assert.AreEqual("test", "test");
    [TestMethod] public void Test05() => Assert.AreNotEqual(1, 2);
    [TestMethod] public void Test06() => Assert.IsTrue(5 > 3);
    [TestMethod] public void Test07() => Assert.IsFalse(2 > 5);
    [TestMethod] public void Test08() => Assert.AreEqual(10, TestOperations.Add(5, 5));
    [TestMethod] public void Test09() => Assert.AreEqual(0, TestOperations.Subtract(5, 5));
    [TestMethod] public void Test10() => Assert.AreEqual(25, TestOperations.Multiply(5, 5));
    [TestMethod] public void Test11() => Assert.IsTrue(true);
    [TestMethod] public void Test12() => Assert.IsFalse(false);
    [TestMethod] public void Test13() => Assert.AreEqual(2, 2);
    [TestMethod] public void Test14() => Assert.AreEqual("hello", "hello");
    [TestMethod] public void Test15() => Assert.AreNotEqual(3, 4);
    [TestMethod] public void Test16() => Assert.IsTrue(10 > 5);
    [TestMethod] public void Test17() => Assert.IsFalse(3 > 10);
    [TestMethod] public void Test18() => Assert.AreEqual(8, TestOperations.Add(3, 5));
    [TestMethod] public void Test19() => Assert.AreEqual(2, TestOperations.Subtract(5, 3));
    [TestMethod] public void Test20() => Assert.AreEqual(15, TestOperations.Multiply(3, 5));
    [TestMethod] public void Test21() => Assert.IsTrue(true);
    [TestMethod] public void Test22() => Assert.IsFalse(false);
    [TestMethod] public void Test23() => Assert.AreEqual(3, 3);
    [TestMethod] public void Test24() => Assert.AreEqual("world", "world");
    [TestMethod] public void Test25() => Assert.AreNotEqual(5, 6);
    [TestMethod] public void Test26() => Assert.IsTrue(15 > 10);
    [TestMethod] public void Test27() => Assert.IsFalse(5 > 15);
    [TestMethod] public void Test28() => Assert.AreEqual(12, TestOperations.Add(7, 5));
    [TestMethod] public void Test29() => Assert.AreEqual(2, TestOperations.Subtract(7, 5));
    [TestMethod] public void Test30() => Assert.AreEqual(35, TestOperations.Multiply(7, 5));
    [TestMethod] public void Test31() => Assert.IsTrue(true);
    [TestMethod] public void Test32() => Assert.IsFalse(false);
    [TestMethod] public void Test33() => Assert.AreEqual(4, 4);
    [TestMethod] public void Test34() => Assert.AreEqual("test", "test");
    [TestMethod] public void Test35() => Assert.AreNotEqual(7, 8);
    [TestMethod] public void Test36() => Assert.IsTrue(20 > 15);
    [TestMethod] public void Test37() => Assert.IsFalse(10 > 20);
    [TestMethod] public void Test38() => Assert.AreEqual(15, TestOperations.Add(10, 5));
    [TestMethod] public void Test39() => Assert.AreEqual(5, TestOperations.Subtract(10, 5));
    [TestMethod] public void Test40() => Assert.AreEqual(50, TestOperations.Multiply(10, 5));
    [TestMethod] public void Test41() => Assert.IsTrue(true);
    [TestMethod] public void Test42() => Assert.IsFalse(false);
    [TestMethod] public void Test43() => Assert.AreEqual(5, 5);
    [TestMethod] public void Test44() => Assert.AreEqual("data", "data");
    [TestMethod] public void Test45() => Assert.AreNotEqual(9, 10);
    [TestMethod] public void Test46() => Assert.IsTrue(25 > 20);
    [TestMethod] public void Test47() => Assert.IsFalse(15 > 25);
    [TestMethod] public void Test48() => Assert.AreEqual(20, TestOperations.Add(15, 5));
    [TestMethod] public void Test49() => Assert.AreEqual(10, TestOperations.Subtract(15, 5));
    [TestMethod] public void Test50() => Assert.AreEqual(75, TestOperations.Multiply(15, 5));
}

[TestClass]
public class ParameterizedTests
{
    [DataTestMethod]
    [DataRow(1, 2, 3)] [DataRow(5, 7, 12)] [DataRow(-1, 1, 0)] [DataRow(0, 0, 0)] [DataRow(100, 200, 300)]
    [DataRow(-50, -50, -100)] [DataRow(42, 58, 100)] [DataRow(999, 1, 1000)] [DataRow(-100, 100, 0)] [DataRow(7, 3, 10)]
    public void Addition_ReturnsCorrectSum(int a, int b, int expected) => Assert.AreEqual(expected, TestOperations.Add(a, b));

    [DataTestMethod]
    [DataRow("hello", 5)] [DataRow("world", 5)] [DataRow("", 0)] [DataRow("a", 1)] [DataRow("test case", 9)]
    [DataRow("benchmark", 9)] [DataRow("NextUnit", 8)] [DataRow("x", 1)] [DataRow("testing framework", 17)] [DataRow("ab", 2)]
    public void StringLength_IsCorrect(string text, int expectedLength) => Assert.AreEqual(expectedLength, TestOperations.GetLength(text));

    [DataTestMethod]
    [DataRow(5, 1, 10)] [DataRow(50, 0, 100)] [DataRow(0, -10, 10)] [DataRow(99, 90, 100)] [DataRow(-5, -10, 0)]
    [DataRow(25, 20, 30)] [DataRow(1, 1, 1)] [DataRow(75, 50, 100)] [DataRow(42, 40, 45)] [DataRow(10, 5, 15)]
    public void IsInRange_ReturnsTrue(int value, int min, int max) => Assert.IsTrue(TestOperations.IsInRange(value, min, max));

    [DataTestMethod]
    [DataRow(2, 1, 1)] [DataRow(5, 3, 2)] [DataRow(10, 5, 5)] [DataRow(100, 50, 50)] [DataRow(0, 0, 0)]
    [DataRow(-5, -3, -2)] [DataRow(20, 10, 10)] [DataRow(7, 4, 3)] [DataRow(15, 8, 7)] [DataRow(30, 20, 10)]
    public void Subtraction_ReturnsCorrectResult(int a, int b, int expected) => Assert.AreEqual(expected, TestOperations.Subtract(a, b));

    [DataTestMethod]
    [DataRow(2, 3, 6)] [DataRow(5, 5, 25)] [DataRow(10, 10, 100)] [DataRow(0, 100, 0)] [DataRow(1, 1, 1)]
    [DataRow(-2, 3, -6)] [DataRow(7, 8, 56)] [DataRow(9, 9, 81)] [DataRow(4, 5, 20)] [DataRow(6, 7, 42)]
    public void Multiplication_ReturnsCorrectResult(int a, int b, int expected) => Assert.AreEqual(expected, TestOperations.Multiply(a, b));
}

[TestClass]
public class LifecycleTests
{
    private int _setupCount;

    [TestInitialize]
    public void Setup()
    {
        TestOperations.PerformSetup();
        _setupCount = 1;
    }

    [TestCleanup]
    public void Cleanup() => TestOperations.PerformCleanup();

    [TestMethod] public void LifecycleTest01() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest02() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest03() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest04() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest05() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest06() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest07() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest08() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest09() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest10() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest11() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest12() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest13() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest14() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest15() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest16() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest17() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest18() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest19() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest20() => Assert.IsTrue(_setupCount > 0);
    [TestMethod] public void LifecycleTest21() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest22() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest23() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest24() => Assert.AreEqual(1, _setupCount);
    [TestMethod] public void LifecycleTest25() => Assert.AreEqual(1, _setupCount);
}

[TestClass]
public class AsyncTests
{
    [TestMethod] public async Task AsyncTest01() => Assert.AreEqual(42, await TestOperations.GetValueAsync());
    [TestMethod] public async Task AsyncTest02() => Assert.IsTrue(await TestOperations.GetValueAsync() > 0);
    [TestMethod] public async Task AsyncTest03() => Assert.AreEqual("result", await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest04() => Assert.IsNotNull(await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest05() { var v = await TestOperations.GetValueAsync(); Assert.IsTrue(v >= 0 && v <= 100); }
    [TestMethod] public async Task AsyncTest06() => Assert.AreEqual(42, await TestOperations.GetValueAsync());
    [TestMethod] public async Task AsyncTest07() => Assert.IsTrue(await TestOperations.GetValueAsync() > 0);
    [TestMethod] public async Task AsyncTest08() => Assert.AreEqual("result", await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest09() => Assert.IsNotNull(await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest10() { var v = await TestOperations.GetValueAsync(); Assert.IsTrue(v >= 0 && v <= 100); }
    [TestMethod] public async Task AsyncTest11() => Assert.AreEqual(42, await TestOperations.GetValueAsync());
    [TestMethod] public async Task AsyncTest12() => Assert.IsTrue(await TestOperations.GetValueAsync() > 0);
    [TestMethod] public async Task AsyncTest13() => Assert.AreEqual("result", await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest14() => Assert.IsNotNull(await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest15() { var v = await TestOperations.GetValueAsync(); Assert.IsTrue(v >= 0 && v <= 100); }
    [TestMethod] public async Task AsyncTest16() => Assert.AreEqual(42, await TestOperations.GetValueAsync());
    [TestMethod] public async Task AsyncTest17() => Assert.IsTrue(await TestOperations.GetValueAsync() > 0);
    [TestMethod] public async Task AsyncTest18() => Assert.AreEqual("result", await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest19() => Assert.IsNotNull(await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest20() { var v = await TestOperations.GetValueAsync(); Assert.IsTrue(v >= 0 && v <= 100); }
    [TestMethod] public async Task AsyncTest21() => Assert.AreEqual(42, await TestOperations.GetValueAsync());
    [TestMethod] public async Task AsyncTest22() => Assert.IsTrue(await TestOperations.GetValueAsync() > 0);
    [TestMethod] public async Task AsyncTest23() => Assert.AreEqual("result", await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest24() => Assert.IsNotNull(await TestOperations.GetStringAsync());
    [TestMethod] public async Task AsyncTest25() { var v = await TestOperations.GetValueAsync(); Assert.IsTrue(v >= 0 && v <= 100); }
}

[TestClass]
public class ComplexAssertionTests
{
    [TestMethod] public void Collection_Contains() => CollectionAssert.Contains(SharedTestData.NumberCollection.ToList(), 5);
    [TestMethod] public void Collection_DoesNotContain() => CollectionAssert.DoesNotContain(SharedTestData.NumberCollection.ToList(), 99);
    [TestMethod] public void Collection_NotEmpty() => Assert.IsTrue(SharedTestData.NumberCollection.Any());
    [TestMethod] public void String_Contains() => StringAssert.Contains("hello", "ell");
    [TestMethod] public void String_StartsWith() => StringAssert.StartsWith("hello", "hel");
    [TestMethod] public void String_EndsWith() => StringAssert.EndsWith("hello", "llo");
    [TestMethod] public void Numeric_InRange() => Assert.IsTrue(5 >= 1 && 5 <= 10);
    [TestMethod] public void Numeric_NotInRange() => Assert.IsFalse(15 >= 1 && 15 <= 10);
    [TestMethod] public void Collection_All() => Assert.IsTrue(SharedTestData.NumberCollection.All(n => n > 0));
    [TestMethod] public void StringCollection_Contains() => CollectionAssert.Contains(SharedTestData.StringCollection.ToList(), "apple");
    [TestMethod] public void StringCollection_NotEmpty() => Assert.IsTrue(SharedTestData.StringCollection.Any());
    [TestMethod] public void Collection_Contains2() => CollectionAssert.Contains(SharedTestData.NumberCollection.ToList(), 1);
    [TestMethod] public void Collection_Contains3() => CollectionAssert.Contains(SharedTestData.NumberCollection.ToList(), 10);
    [TestMethod] public void String_Contains2() => StringAssert.Contains("world", "orl");
    [TestMethod] public void String_StartsWith2() => StringAssert.StartsWith("world", "wor");
    [TestMethod] public void String_EndsWith2() => StringAssert.EndsWith("world", "rld");
    [TestMethod] public void Numeric_InRange2() => Assert.IsTrue(50 >= 0 && 50 <= 100);
    [TestMethod] public void Numeric_NotInRange2() => Assert.IsFalse(150 >= 0 && 150 <= 100);
    [TestMethod] public void Collection_All2() => Assert.IsTrue(SharedTestData.NumberCollection.All(n => n < 100));
    [TestMethod] public void StringCollection_Contains2() => CollectionAssert.Contains(SharedTestData.StringCollection.ToList(), "banana");
    [TestMethod] public void Collection_Contains4() => CollectionAssert.Contains(SharedTestData.NumberCollection.ToList(), 7);
    [TestMethod] public void String_Contains3() => StringAssert.Contains("test", "st");
    [TestMethod] public void Numeric_InRange3() => Assert.IsTrue(75 >= 50 && 75 <= 100);
    [TestMethod] public void Collection_All3() => Assert.IsTrue(SharedTestData.NumberCollection.All(n => n >= 1 && n <= 10));
    [TestMethod] public void StringCollection_Contains3() => CollectionAssert.Contains(SharedTestData.StringCollection.ToList(), "cherry");
}

[TestClass]
public class ParallelTests
{
    [TestMethod] public void ParallelTest01() => Assert.AreEqual(2, TestOperations.Add(1, 1));
    [TestMethod] public void ParallelTest02() => Assert.AreEqual(4, TestOperations.Add(2, 2));
    [TestMethod] public void ParallelTest03() => Assert.AreEqual(6, TestOperations.Add(3, 3));
    [TestMethod] public void ParallelTest04() => Assert.AreEqual(8, TestOperations.Add(4, 4));
    [TestMethod] public void ParallelTest05() => Assert.AreEqual(10, TestOperations.Add(5, 5));
    [TestMethod] public void ParallelTest06() => Assert.AreEqual(12, TestOperations.Add(6, 6));
    [TestMethod] public void ParallelTest07() => Assert.AreEqual(14, TestOperations.Add(7, 7));
    [TestMethod] public void ParallelTest08() => Assert.AreEqual(16, TestOperations.Add(8, 8));
    [TestMethod] public void ParallelTest09() => Assert.AreEqual(18, TestOperations.Add(9, 9));
    [TestMethod] public void ParallelTest10() => Assert.AreEqual(20, TestOperations.Add(10, 10));
    [TestMethod] public void ParallelTest11() => Assert.AreEqual(0, TestOperations.Subtract(5, 5));
    [TestMethod] public void ParallelTest12() => Assert.AreEqual(0, TestOperations.Subtract(10, 10));
    [TestMethod] public void ParallelTest13() => Assert.AreEqual(0, TestOperations.Subtract(15, 15));
    [TestMethod] public void ParallelTest14() => Assert.AreEqual(0, TestOperations.Subtract(20, 20));
    [TestMethod] public void ParallelTest15() => Assert.AreEqual(0, TestOperations.Subtract(25, 25));
    [TestMethod] public void ParallelTest16() => Assert.AreEqual(25, TestOperations.Multiply(5, 5));
    [TestMethod] public void ParallelTest17() => Assert.AreEqual(36, TestOperations.Multiply(6, 6));
    [TestMethod] public void ParallelTest18() => Assert.AreEqual(49, TestOperations.Multiply(7, 7));
    [TestMethod] public void ParallelTest19() => Assert.AreEqual(64, TestOperations.Multiply(8, 8));
    [TestMethod] public void ParallelTest20() => Assert.AreEqual(81, TestOperations.Multiply(9, 9));
    [TestMethod] public void ParallelTest21() => Assert.IsTrue(TestOperations.IsInRange(5, 1, 10));
    [TestMethod] public void ParallelTest22() => Assert.IsTrue(TestOperations.IsInRange(50, 0, 100));
    [TestMethod] public void ParallelTest23() => Assert.IsTrue(TestOperations.IsInRange(25, 20, 30));
    [TestMethod] public void ParallelTest24() => Assert.IsTrue(TestOperations.IsInRange(75, 50, 100));
    [TestMethod] public void ParallelTest25() => Assert.IsTrue(TestOperations.IsInRange(10, 5, 15));
}
