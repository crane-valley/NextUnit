using System.Diagnostics;
using BenchmarkDotNet.Attributes;

namespace NextUnit.Benchmarks;

/// <summary>
/// Benchmarks comparing NextUnit performance with baseline measurements
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class TestSuiteExecutionBenchmarks
{
    private const string LargeTestSuitePath = "../../samples/NextUnit.LargeTestSuite/NextUnit.LargeTestSuite.csproj";
    private const string SampleTestsPath = "../../samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj";

    [Benchmark(Description = "NextUnit: 1000 tests execution time")]
    public async Task<TimeSpan> NextUnit_1000Tests()
    {
        var sw = Stopwatch.StartNew();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build --project {LargeTestSuitePath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start process");

        await process.WaitForExitAsync();
        sw.Stop();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Test execution failed: {error}");
        }

        return sw.Elapsed;
    }

    [Benchmark(Description = "NextUnit: 125 tests execution time")]
    public async Task<TimeSpan> NextUnit_125Tests()
    {
        var sw = Stopwatch.StartNew();
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build --project {SampleTestsPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start process");

        await process.WaitForExitAsync();
        sw.Stop();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Test execution failed: {error}");
        }

        return sw.Elapsed;
    }
}
