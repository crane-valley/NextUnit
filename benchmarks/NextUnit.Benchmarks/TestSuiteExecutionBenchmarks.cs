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
    private static readonly string _repositoryRoot = GetRepositoryRoot();
    private static readonly string _largeTestSuitePath = Path.Combine(_repositoryRoot, "samples", "NextUnit.LargeTestSuite", "NextUnit.LargeTestSuite.csproj");
    private static readonly string _sampleTestsPath = Path.Combine(_repositoryRoot, "samples", "NextUnit.SampleTests", "NextUnit.SampleTests.csproj");

    /// <summary>
    /// Locates the repository root by walking up from the assembly location.
    /// This approach works regardless of where BenchmarkDotNet places its output artifacts.
    /// </summary>
    private static string GetRepositoryRoot()
    {
        // Start from the benchmark assembly location and walk up to find the repository root
        var assemblyLocation = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(assemblyLocation);

        while (directory != null)
        {
            // Look for a marker file that exists in the repository root (e.g., .git directory or a specific file)
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                File.Exists(Path.Combine(directory.FullName, "NextUnit.slnx")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        // Fallback: assume we're 3 levels deep from the repo root (benchmarks/NextUnit.Benchmarks/bin)
        throw new InvalidOperationException("Could not locate repository root. Ensure the benchmark is run from within the NextUnit repository.");
    }

    [Benchmark(Description = "NextUnit: 1000 tests execution time")]
    public async Task NextUnit_1000TestsAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build --project {_largeTestSuitePath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start process");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Test execution failed: {error}");
        }
    }

    [Benchmark(Description = "NextUnit: 125 tests execution time")]
    public async Task NextUnit_125TestsAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --no-build --project {_sampleTestsPath}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start process");

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"Test execution failed: {error}");
        }
    }
}
