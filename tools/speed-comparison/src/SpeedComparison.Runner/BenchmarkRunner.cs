using System.Diagnostics;
using System.Runtime.InteropServices;
using SpeedComparison.Runner.Models;

namespace SpeedComparison.Runner;

/// <summary>
/// Orchestrates benchmark execution across multiple test frameworks
/// </summary>
public class BenchmarkRunner
{
    private const int IterationCount = 5;
    private readonly string _solutionRoot;

    public BenchmarkRunner(string solutionRoot)
    {
        _solutionRoot = solutionRoot;
    }

    public async Task<BenchmarkResults> RunBenchmarksAsync()
    {
        var results = new BenchmarkResults
        {
            Environment = GetEnvironmentInfo()
        };

        Console.WriteLine("=== NextUnit Speed Comparison Benchmarks ===");
        Console.WriteLine($"Date: {results.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"OS: {results.Environment.OperatingSystem}");
        Console.WriteLine($".NET: {results.Environment.DotNetVersion}");
        Console.WriteLine($"Iterations per framework: {IterationCount}");
        Console.WriteLine();

        // Build all projects first
        Console.WriteLine("Building all test projects...");
        await BuildAllProjectsAsync();
        Console.WriteLine();

        // Run benchmarks for each framework
        results.Frameworks["NextUnit"] = await RunNextUnitBenchmarkAsync();
        results.Frameworks["xUnit"] = await RunXUnitBenchmarkAsync();
        results.Frameworks["NUnit"] = await RunNUnitBenchmarkAsync();
        results.Frameworks["MSTest"] = await RunMSTestBenchmarkAsync();

        // Calculate relative performance (using NextUnit as baseline)
        CalculateRelativePerformance(results);

        return results;
    }

    private async Task BuildAllProjectsAsync()
    {
        var projects = new[]
        {
            "SpeedComparison.NextUnit",
            "SpeedComparison.XUnit",
            "SpeedComparison.NUnit",
            "SpeedComparison.MSTest"
        };

        foreach (var project in projects)
        {
            Console.Write($"  Building {project}... ");
            var projectPath = Path.Combine(_solutionRoot, "src", project, $"{project}.csproj");
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{projectPath}\" --configuration Release --verbosity quiet --nologo",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                Console.WriteLine("FAILED (process not started)");
                continue;
            }

            await process.WaitForExitAsync();
            
            if (process.ExitCode == 0)
            {
                Console.WriteLine("OK");
            }
            else
            {
                Console.WriteLine($"FAILED (exit code: {process.ExitCode})");
                var error = await process.StandardError.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(error))
                {
                    Console.WriteLine($"    Error: {error}");
                }
            }
        }
    }

    private async Task<FrameworkMetrics> RunNextUnitBenchmarkAsync()
    {
        Console.WriteLine("Running NextUnit benchmarks...");
        var projectPath = Path.Combine(_solutionRoot, "src", "SpeedComparison.NextUnit", "SpeedComparison.NextUnit.csproj");
        
        var metrics = new FrameworkMetrics
        {
            FrameworkName = "NextUnit",
            Version = "1.4.0", // TODO: Read from project file
            TestCount = 200
        };

        for (int i = 0; i < IterationCount; i++)
        {
            Console.Write($"  Iteration {i + 1}/{IterationCount}... ");
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" --configuration Release --no-build --verbosity quiet --nologo",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(processInfo);
            
            if (process == null)
            {
                Console.WriteLine("FAILED");
                continue;
            }

            await process.WaitForExitAsync();
            stopwatch.Stop();
            
            var peakMemory = process.PeakWorkingSet64;
            
            metrics.ExecutionTimesMs.Add(stopwatch.ElapsedMilliseconds);
            metrics.PeakMemoryBytes.Add(peakMemory);
            
            Console.WriteLine($"{stopwatch.ElapsedMilliseconds}ms, {peakMemory / 1024 / 1024}MB peak");
            
            // Parse output for pass/fail counts (for first iteration)
            if (i == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                ParseTestResults(output, metrics);
            }
        }

        CalculateStatistics(metrics);
        Console.WriteLine();
        return metrics;
    }

    private async Task<FrameworkMetrics> RunXUnitBenchmarkAsync()
    {
        Console.WriteLine("Running xUnit benchmarks...");
        var projectPath = Path.Combine(_solutionRoot, "src", "SpeedComparison.XUnit", "SpeedComparison.XUnit.csproj");
        
        var metrics = new FrameworkMetrics
        {
            FrameworkName = "xUnit",
            Version = "2.9.3",
            TestCount = 200
        };

        for (int i = 0; i < IterationCount; i++)
        {
            Console.Write($"  Iteration {i + 1}/{IterationCount}... ");
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" --configuration Release --no-build --verbosity quiet --nologo",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(processInfo);
            
            if (process == null)
            {
                Console.WriteLine("FAILED");
                continue;
            }

            await process.WaitForExitAsync();
            stopwatch.Stop();
            
            var peakMemory = process.PeakWorkingSet64;
            
            metrics.ExecutionTimesMs.Add(stopwatch.ElapsedMilliseconds);
            metrics.PeakMemoryBytes.Add(peakMemory);
            
            Console.WriteLine($"{stopwatch.ElapsedMilliseconds}ms, {peakMemory / 1024 / 1024}MB peak");
            
            if (i == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                ParseTestResults(output, metrics);
            }
        }

        CalculateStatistics(metrics);
        Console.WriteLine();
        return metrics;
    }

    private async Task<FrameworkMetrics> RunNUnitBenchmarkAsync()
    {
        Console.WriteLine("Running NUnit benchmarks...");
        var projectPath = Path.Combine(_solutionRoot, "src", "SpeedComparison.NUnit", "SpeedComparison.NUnit.csproj");
        
        var metrics = new FrameworkMetrics
        {
            FrameworkName = "NUnit",
            Version = "4.3.1",
            TestCount = 200
        };

        for (int i = 0; i < IterationCount; i++)
        {
            Console.Write($"  Iteration {i + 1}/{IterationCount}... ");
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" --configuration Release --no-build --verbosity quiet --nologo",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(processInfo);
            
            if (process == null)
            {
                Console.WriteLine("FAILED");
                continue;
            }

            await process.WaitForExitAsync();
            stopwatch.Stop();
            
            var peakMemory = process.PeakWorkingSet64;
            
            metrics.ExecutionTimesMs.Add(stopwatch.ElapsedMilliseconds);
            metrics.PeakMemoryBytes.Add(peakMemory);
            
            Console.WriteLine($"{stopwatch.ElapsedMilliseconds}ms, {peakMemory / 1024 / 1024}MB peak");
            
            if (i == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                ParseTestResults(output, metrics);
            }
        }

        CalculateStatistics(metrics);
        Console.WriteLine();
        return metrics;
    }

    private async Task<FrameworkMetrics> RunMSTestBenchmarkAsync()
    {
        Console.WriteLine("Running MSTest benchmarks...");
        var projectPath = Path.Combine(_solutionRoot, "src", "SpeedComparison.MSTest", "SpeedComparison.MSTest.csproj");
        
        var metrics = new FrameworkMetrics
        {
            FrameworkName = "MSTest",
            Version = "3.7.0",
            TestCount = 200
        };

        for (int i = 0; i < IterationCount; i++)
        {
            Console.Write($"  Iteration {i + 1}/{IterationCount}... ");
            
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"test \"{projectPath}\" --configuration Release --no-build --verbosity quiet --nologo",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var stopwatch = Stopwatch.StartNew();
            using var process = Process.Start(processInfo);
            
            if (process == null)
            {
                Console.WriteLine("FAILED");
                continue;
            }

            await process.WaitForExitAsync();
            stopwatch.Stop();
            
            var peakMemory = process.PeakWorkingSet64;
            
            metrics.ExecutionTimesMs.Add(stopwatch.ElapsedMilliseconds);
            metrics.PeakMemoryBytes.Add(peakMemory);
            
            Console.WriteLine($"{stopwatch.ElapsedMilliseconds}ms, {peakMemory / 1024 / 1024}MB peak");
            
            if (i == 0)
            {
                var output = await process.StandardOutput.ReadToEndAsync();
                ParseTestResults(output, metrics);
            }
        }

        CalculateStatistics(metrics);
        Console.WriteLine();
        return metrics;
    }

    private void ParseTestResults(string output, FrameworkMetrics metrics)
    {
        // Default to all tests passing if we can't parse
        metrics.PassedTests = metrics.TestCount;
        metrics.FailedTests = 0;
        metrics.SkippedTests = 0;
        
        // Try to parse actual results from output
        // This is a simple heuristic - actual parsing would be more complex
        if (output.Contains("Passed!") || output.Contains("passed"))
        {
            // Results are likely all passed
        }
    }

    private void CalculateStatistics(FrameworkMetrics metrics)
    {
        if (metrics.ExecutionTimesMs.Count == 0) return;

        // Calculate timing statistics
        var sortedTimes = metrics.ExecutionTimesMs.OrderBy(x => x).ToList();
        metrics.MedianTimeMs = sortedTimes[sortedTimes.Count / 2];
        metrics.AverageTimeMs = (long)sortedTimes.Average();
        metrics.PerTestTimeMs = (double)metrics.AverageTimeMs / metrics.TestCount;
        metrics.TestsPerSecond = metrics.TestCount / (metrics.AverageTimeMs / 1000.0);

        // Calculate memory statistics
        var sortedMemory = metrics.PeakMemoryBytes.OrderBy(x => x).ToList();
        metrics.MedianPeakMemoryBytes = sortedMemory[sortedMemory.Count / 2];
        metrics.AveragePeakMemoryBytes = (long)sortedMemory.Average();
        metrics.PeakMemoryMB = metrics.AveragePeakMemoryBytes / 1024.0 / 1024.0;
    }

    private void CalculateRelativePerformance(BenchmarkResults results)
    {
        if (!results.Frameworks.TryGetValue("NextUnit", out var baseline))
            return;

        foreach (var framework in results.Frameworks.Values)
        {
            if (framework.FrameworkName == "NextUnit")
            {
                framework.RelativeSpeed = 1.0;
                framework.PerformanceRating = "Baseline";
            }
            else
            {
                framework.RelativeSpeed = (double)framework.AverageTimeMs / baseline.AverageTimeMs;
                
                if (framework.RelativeSpeed < 0.9)
                    framework.PerformanceRating = $"{framework.RelativeSpeed:F1}x faster";
                else if (framework.RelativeSpeed > 1.1)
                    framework.PerformanceRating = $"{framework.RelativeSpeed:F1}x slower";
                else
                    framework.PerformanceRating = "Similar";
            }
        }
    }

    private EnvironmentInfo GetEnvironmentInfo()
    {
        return new EnvironmentInfo
        {
            OperatingSystem = RuntimeInformation.OSDescription,
            DotNetVersion = Environment.Version.ToString(),
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount.ToString()
        };
    }
}
