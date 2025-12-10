namespace SpeedComparison.Runner.Models;

/// <summary>
/// Represents the complete benchmark results
/// </summary>
public class BenchmarkResults
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public EnvironmentInfo Environment { get; set; } = new();
    public Dictionary<string, FrameworkMetrics> Frameworks { get; set; } = new();
}

/// <summary>
/// Environment information for the benchmark run
/// </summary>
public class EnvironmentInfo
{
    public string OperatingSystem { get; set; } = string.Empty;
    public string DotNetVersion { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string ProcessorCount { get; set; } = string.Empty;
}

/// <summary>
/// Performance metrics for a specific test framework
/// </summary>
public class FrameworkMetrics
{
    public string FrameworkName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int TestCount { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public int SkippedTests { get; set; }
    
    // Timing metrics (milliseconds)
    public List<long> ExecutionTimesMs { get; set; } = new();
    public long MedianTimeMs { get; set; }
    public long AverageTimeMs { get; set; }
    public double PerTestTimeMs { get; set; }
    public double TestsPerSecond { get; set; }
    
    // Memory metrics (bytes)
    public List<long> PeakMemoryBytes { get; set; } = new();
    public long MedianPeakMemoryBytes { get; set; }
    public long AveragePeakMemoryBytes { get; set; }
    public double PeakMemoryMB { get; set; }
    
    // Relative performance (compared to baseline)
    public double RelativeSpeed { get; set; } = 1.0; // 1.0 = baseline, 2.0 = 2x slower, 0.5 = 2x faster
    public string PerformanceRating { get; set; } = "Baseline";
}
