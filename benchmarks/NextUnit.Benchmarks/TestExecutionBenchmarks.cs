using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using NextUnit.Core;
using NextUnit.Internal;

namespace NextUnit.Benchmarks;

[SimpleJob(RunStrategy.Monitoring, iterationCount: 10)]
[MemoryDiagnoser]
public class TestExecutionBenchmarks
{
    private static readonly ITestExecutionSink _sink = new NullSink();
    private TestCaseDescriptor[] _tests = [];

    [Params(100, 1_000, 10_000)]
    public int TestCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tests = Enumerable.Range(0, TestCount)
            .Select(static index => new TestCaseDescriptor
            {
                Id = new TestCaseId($"BenchmarkTests.Test{index}"),
                DisplayName = $"Test{index}",
                TestClass = typeof(BenchmarkTestClass),
                MethodName = nameof(BenchmarkTestClass.Run),
                TestClassFactory = static (_, _) => new BenchmarkTestClass(),
                TestMethod = static (instance, cancellationToken) =>
                    ((BenchmarkTestClass)instance).Run(cancellationToken)
            })
            .ToArray();
    }

    [Benchmark(Description = "Execute tests through the NextUnit engine")]
    public Task ExecuteTestsAsync() =>
        new TestExecutionEngine().RunAsync(_tests, _sink, CancellationToken.None);

    private sealed class BenchmarkTestClass
    {
        public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NullSink : ITestExecutionSink
    {
        public Task ReportPassedAsync(
            TestCaseDescriptor test,
            string? output = null,
            IReadOnlyList<Artifact>? artifacts = null) =>
            Task.CompletedTask;

        public Task ReportFailedAsync(
            TestCaseDescriptor test,
            AssertionFailedException ex,
            string? output = null,
            IReadOnlyList<Artifact>? artifacts = null) =>
            Task.CompletedTask;

        public Task ReportErrorAsync(
            TestCaseDescriptor test,
            Exception ex,
            string? output = null,
            IReadOnlyList<Artifact>? artifacts = null) =>
            Task.CompletedTask;

        public Task ReportSkippedAsync(
            TestCaseDescriptor test,
            IReadOnlyList<Artifact>? artifacts = null) =>
            Task.CompletedTask;
    }
}
