namespace UnifiedTests;

[TestClass]
public class ScaleTests
{
    // Small scale tests (fast individual tests)
    [Test] public void SmallTest_001() => PerformComputation(1);
    [Test] public void SmallTest_002() => PerformComputation(2);
    [Test] public void SmallTest_003() => PerformComputation(3);
    [Test] public void SmallTest_004() => PerformComputation(4);
    [Test] public void SmallTest_005() => PerformComputation(5);
    [Test] public void SmallTest_006() => PerformComputation(6);
    [Test] public void SmallTest_007() => PerformComputation(7);
    [Test] public void SmallTest_008() => PerformComputation(8);
    [Test] public void SmallTest_009() => PerformComputation(9);
    [Test] public void SmallTest_010() => PerformComputation(10);

    // Medium scale tests (more computation)
    [Test] public void MediumTest_001() => PerformComputation(100);
    [Test] public void MediumTest_002() => PerformComputation(200);
    [Test] public void MediumTest_003() => PerformComputation(300);
    [Test] public void MediumTest_004() => PerformComputation(400);
    [Test] public void MediumTest_005() => PerformComputation(500);

    // Large scale tests (significant computation)
    [Test] public void LargeTest_001() => PerformComputation(1000);
    [Test] public void LargeTest_002() => PerformComputation(2000);
    [Test] public void LargeTest_003() => PerformComputation(3000);

    private void PerformComputation(int iterations)
    {
        var sum = 0;
        for (int i = 0; i < iterations; i++)
        {
            sum += i * i;
        }
        // Verify computation happened
        _ = sum > 0;
    }
}
