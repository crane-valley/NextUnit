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
    private static readonly string _sampleTestsPath = Path.Combine(_repositoryRoot, "samples", "NextUnit.SampleTests", "NextUnit.SampleTests.csproj");

    [GlobalSetup]
    public Task BuildSampleSuiteAsync() =>
        RunDotnetAsync(["build", _sampleTestsPath, "--configuration", "Release"]);

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

    [Benchmark(Description = "NextUnit sample suite process execution")]
    public Task NextUnitSampleTestsAsync() =>
        RunDotnetAsync(
            [
                "run",
                "--configuration",
                "Release",
                "--no-build",
                "--project",
                _sampleTestsPath,
                "--",
                "--minimum-expected-tests",
                "20"
            ]);

    private static async Task RunDotnetAsync(IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start process");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            throw new InvalidOperationException($"Test execution failed: {error}");
        }

        _ = await outputTask;
    }
}
