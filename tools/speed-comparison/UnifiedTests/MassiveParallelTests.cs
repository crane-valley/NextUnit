namespace UnifiedTests;

[TestClass]
public class MassiveParallelTests
{
    // Tests designed to run in parallel to test framework's parallel execution efficiency
    [Test] public void ParallelTest_001() => DoWork(1);
    [Test] public void ParallelTest_002() => DoWork(2);
    [Test] public void ParallelTest_003() => DoWork(3);
    [Test] public void ParallelTest_004() => DoWork(4);
    [Test] public void ParallelTest_005() => DoWork(5);
    [Test] public void ParallelTest_006() => DoWork(6);
    [Test] public void ParallelTest_007() => DoWork(7);
    [Test] public void ParallelTest_008() => DoWork(8);
    [Test] public void ParallelTest_009() => DoWork(9);
    [Test] public void ParallelTest_010() => DoWork(10);
    [Test] public void ParallelTest_011() => DoWork(11);
    [Test] public void ParallelTest_012() => DoWork(12);
    [Test] public void ParallelTest_013() => DoWork(13);
    [Test] public void ParallelTest_014() => DoWork(14);
    [Test] public void ParallelTest_015() => DoWork(15);
    [Test] public void ParallelTest_016() => DoWork(16);
    [Test] public void ParallelTest_017() => DoWork(17);
    [Test] public void ParallelTest_018() => DoWork(18);
    [Test] public void ParallelTest_019() => DoWork(19);
    [Test] public void ParallelTest_020() => DoWork(20);
    [Test] public void ParallelTest_021() => DoWork(21);
    [Test] public void ParallelTest_022() => DoWork(22);
    [Test] public void ParallelTest_023() => DoWork(23);
    [Test] public void ParallelTest_024() => DoWork(24);
    [Test] public void ParallelTest_025() => DoWork(25);
    [Test] public void ParallelTest_026() => DoWork(26);
    [Test] public void ParallelTest_027() => DoWork(27);
    [Test] public void ParallelTest_028() => DoWork(28);
    [Test] public void ParallelTest_029() => DoWork(29);
    [Test] public void ParallelTest_030() => DoWork(30);
    [Test] public void ParallelTest_031() => DoWork(31);
    [Test] public void ParallelTest_032() => DoWork(32);
    [Test] public void ParallelTest_033() => DoWork(33);
    [Test] public void ParallelTest_034() => DoWork(34);
    [Test] public void ParallelTest_035() => DoWork(35);
    [Test] public void ParallelTest_036() => DoWork(36);
    [Test] public void ParallelTest_037() => DoWork(37);
    [Test] public void ParallelTest_038() => DoWork(38);
    [Test] public void ParallelTest_039() => DoWork(39);
    [Test] public void ParallelTest_040() => DoWork(40);
    [Test] public void ParallelTest_041() => DoWork(41);
    [Test] public void ParallelTest_042() => DoWork(42);
    [Test] public void ParallelTest_043() => DoWork(43);
    [Test] public void ParallelTest_044() => DoWork(44);
    [Test] public void ParallelTest_045() => DoWork(45);
    [Test] public void ParallelTest_046() => DoWork(46);
    [Test] public void ParallelTest_047() => DoWork(47);
    [Test] public void ParallelTest_048() => DoWork(48);
    [Test] public void ParallelTest_049() => DoWork(49);
    [Test] public void ParallelTest_050() => DoWork(50);

    private void DoWork(int id)
    {
        // Simulate some work - benchmark measures parallel execution overhead
        var sum = 0;
        for (int i = 0; i < 100; i++)
        {
            sum += i * id;
        }
        _ = sum > 0;
    }
}
