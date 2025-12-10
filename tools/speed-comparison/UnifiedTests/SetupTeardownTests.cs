namespace UnifiedTests;

[TestClass]
#if XUNIT
public class SetupTeardownTests : IDisposable
#else
public class SetupTeardownTests
#endif
{
#if NEXTUNIT
    [Before(LifecycleScope.Test)]
    public void Setup()
    {
        // Setup operations
        var data = InitializeTestData();
    }

    [After(LifecycleScope.Test)]
    public void Teardown()
    {
        // Cleanup operations
        CleanupTestData();
    }
#elif XUNIT
    public SetupTeardownTests()
    {
        // Constructor acts as setup in xUnit
        var data = InitializeTestData();
    }

    public void Dispose()
    {
        // Dispose acts as teardown in xUnit
        CleanupTestData();
    }
#elif NUNIT
    [SetUp]
    public void Setup()
    {
        var data = InitializeTestData();
    }

    [TearDown]
    public void Teardown()
    {
        CleanupTestData();
    }
#elif MSTEST
    [TestInitialize]
    public void Setup()
    {
        var data = InitializeTestData();
    }

    [TestCleanup]
    public void Teardown()
    {
        CleanupTestData();
    }
#endif

    [Test]
    public void Test_WithSetupTeardown_001()
    {
        PerformTest(1);
    }

    [Test]
    public void Test_WithSetupTeardown_002()
    {
        PerformTest(2);
    }

    [Test]
    public void Test_WithSetupTeardown_003()
    {
        PerformTest(3);
    }

    [Test]
    public void Test_WithSetupTeardown_004()
    {
        PerformTest(4);
    }

    [Test]
    public void Test_WithSetupTeardown_005()
    {
        PerformTest(5);
    }

    [Test]
    public void Test_WithSetupTeardown_006()
    {
        PerformTest(6);
    }

    [Test]
    public void Test_WithSetupTeardown_007()
    {
        PerformTest(7);
    }

    [Test]
    public void Test_WithSetupTeardown_008()
    {
        PerformTest(8);
    }

    [Test]
    public void Test_WithSetupTeardown_009()
    {
        PerformTest(9);
    }

    [Test]
    public void Test_WithSetupTeardown_010()
    {
        PerformTest(10);
    }

    private object InitializeTestData()
    {
        return new { Value = 42, Name = "Test" };
    }

    private void CleanupTestData()
    {
        // Cleanup logic
    }

    private void PerformTest(int id)
    {
        var result = id * 2;
        var valid = result > 0;
    }
}
