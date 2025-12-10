# NextUnit Speed-Comparison System - Implementation Plan

## Overview

This document outlines the plan to build a comprehensive speed-comparison benchmarking system similar to [TUnit's speed-comparison tool](https://github.com/thomhurst/TUnit/tree/main/tools/speed-comparison). The system will benchmark NextUnit against other popular .NET test frameworks (xUnit, NUnit, MSTest) using identical test cases to provide fair performance comparisons.

## Goals

1. **Fair Comparison**: Use identical test cases across all frameworks
2. **Comprehensive Metrics**: Measure execution time, memory allocations, and per-test overhead
3. **Automated Updates**: GitHub Actions workflow that automatically updates benchmark results
4. **Transparency**: Clear documentation of methodology and reproducible results
5. **Continuous Monitoring**: Track performance over time to detect regressions

## Architecture

### Directory Structure

```
tools/
└── speed-comparison/
    ├── README.md                          # Documentation for the comparison tool
    ├── SpeedComparison.sln                # Solution file for all comparison projects
    ├── src/
    │   ├── SpeedComparison.Shared/        # Shared test data and utilities
    │   │   ├── TestCases.cs               # Common test case definitions
    │   │   ├── SharedTestData.cs          # Shared test data for parameterized tests
    │   │   └── PerformanceTimer.cs        # Timing utilities
    │   ├── SpeedComparison.NextUnit/      # NextUnit test project
    │   │   └── NextUnitTests.cs           # Tests using NextUnit
    │   ├── SpeedComparison.XUnit/         # xUnit test project
    │   │   └── XUnitTests.cs              # Tests using xUnit
    │   ├── SpeedComparison.NUnit/         # NUnit test project
    │   │   └── NUnitTests.cs              # Tests using NUnit
    │   ├── SpeedComparison.MSTest/        # MSTest test project
    │   │   └── MSTestTests.cs             # Tests using MSTest
    │   └── SpeedComparison.Runner/        # Console app to run benchmarks
    │       ├── Program.cs                 # Main entry point
    │       ├── BenchmarkRunner.cs         # Orchestrates benchmark execution
    │       ├── ResultsCollector.cs        # Collects and processes results
    │       ├── MarkdownGenerator.cs       # Generates markdown reports
    │       └── Models/
    │           ├── BenchmarkResult.cs     # Result model
    │           └── FrameworkMetrics.cs    # Framework-specific metrics
    └── results/
        ├── BENCHMARK_RESULTS.md           # Latest benchmark results (auto-updated)
        └── history/                       # Historical results for trend analysis
            └── YYYY-MM-DD_results.json    # Timestamped raw results
```

### Test Case Strategy

To ensure fair comparison, we'll create identical test scenarios across all frameworks:

1. **Simple Tests** (50 tests)
   - Basic assertions (Equal, True, False)
   - No lifecycle hooks
   - Measures baseline framework overhead

2. **Parameterized Tests** (50 tests)
   - Data-driven tests with 10 parameter sets each
   - Tests framework's parameterization efficiency

3. **Lifecycle Tests** (25 tests)
   - Tests with Before/After (Setup/Teardown) hooks
   - Tests class-level lifecycle management

4. **Async Tests** (25 tests)
   - Async/await test methods
   - Tests framework's async handling overhead

5. **Complex Assertions** (25 tests)
   - Collection assertions
   - String assertions
   - Numeric range assertions

6. **Parallel Tests** (25 tests)
   - Tests that can run in parallel
   - Measures parallelization efficiency

**Total: 200 tests per framework** (fair, comprehensive comparison)

## Metrics to Capture

### 1. Execution Time Metrics
- **Total execution time** (including startup, discovery, execution, teardown)
- **Test discovery time** (time to discover all tests)
- **Actual test execution time** (excluding framework overhead)
- **Per-test average time** (total time / number of tests)

### 2. Memory Metrics
- **Peak memory usage** (maximum working set)
- **Total allocations** (heap allocations during execution)
- **GC collections** (Gen0, Gen1, Gen2 counts)
- **Per-test allocation average**

### 3. Throughput Metrics
- **Tests per second**
- **Tests per millisecond**

### 4. Comparison Metrics
- **Relative performance** (% faster/slower than baseline)
- **Performance per test category** (simple vs. complex)

## Implementation Phases

### Phase 1: Core Infrastructure (Priority 1)

**Files to Create:**
1. `tools/speed-comparison/README.md` - Documentation
2. `tools/speed-comparison/src/SpeedComparison.Shared/` - Shared test cases
3. `tools/speed-comparison/src/SpeedComparison.Runner/` - Benchmark orchestrator

**Key Classes:**
- `TestCaseDefinition` - Represents a single test case
- `BenchmarkRunner` - Executes benchmarks for each framework
- `ResultsCollector` - Aggregates results
- `MarkdownGenerator` - Generates reports

### Phase 2: Framework Test Projects (Priority 2)

**Test Projects:**
1. `SpeedComparison.NextUnit` - Uses NextUnit attributes and assertions
2. `SpeedComparison.XUnit` - Uses xUnit Fact/Theory and Assert
3. `SpeedComparison.NUnit` - Uses NUnit Test and Assert
4. `SpeedComparison.MSTest` - Uses MSTest TestMethod and Assert

**Requirements:**
- Each project implements identical test logic
- Use framework-native features (not adapters)
- Follow framework best practices
- Enable parallel execution where supported

### Phase 3: Benchmark Execution (Priority 3)

**Execution Strategy:**
```csharp
// For each framework:
1. Build test project in Release mode
2. Run tests multiple times (5 iterations)
3. Capture timing using Stopwatch
4. Capture memory using GC and Process APIs
5. Parse test output for pass/fail counts
6. Aggregate results and calculate statistics
```

**Timing Approach:**
```csharp
// Use ProcessStartInfo to run each test framework
var process = Process.Start(new ProcessStartInfo
{
    FileName = "dotnet",
    Arguments = frameworkSpecificArgs,
    RedirectStandardOutput = true,
    RedirectStandardError = true
});

// Measure wall clock time, not CPU time
var stopwatch = Stopwatch.StartNew();
await process.WaitForExitAsync();
stopwatch.Stop();
```

**Memory Measurement:**
```csharp
// Capture peak memory from process
var peakMemory = process.PeakWorkingSet64;

// For allocations, use dotnet-counters or EventPipe
// Alternative: Parse --diagnostics output if available
```

### Phase 4: Results & Reporting (Priority 4)

**Output Format:**

1. **Console Output** (during benchmark run)
```
Running NextUnit benchmarks...
  Iteration 1/5: 1,234ms, 45MB peak memory
  Iteration 2/5: 1,187ms, 43MB peak memory
  ...
Average: 1,210ms, 44MB

Running xUnit benchmarks...
  ...
```

2. **JSON Results** (for trend analysis)
```json
{
  "timestamp": "2025-12-10T03:55:20Z",
  "environment": {
    "os": "Linux",
    "dotnet": "10.0.100",
    "hardware": "x64"
  },
  "frameworks": {
    "nextunit": {
      "version": "1.4.0",
      "totalTime": 1210,
      "avgTimePerTest": 6.05,
      "peakMemoryMB": 44,
      "testsPerSecond": 165
    },
    "xunit": { ... },
    "nunit": { ... },
    "mstest": { ... }
  }
}
```

3. **Markdown Report** (BENCHMARK_RESULTS.md)
```markdown
# Speed Comparison Results

**Last Updated**: 2025-12-10  
**NextUnit Version**: 1.4.0  
**Test Count**: 200 tests per framework

## Summary

| Framework | Version | Total Time | Per-Test Time | Peak Memory | Tests/Sec | Relative Performance |
|-----------|---------|------------|---------------|-------------|-----------|---------------------|
| NextUnit  | 1.4.0   | 1,210ms    | 6.05ms        | 44MB        | 165       | Baseline            |
| xUnit     | 2.9.0   | 2,450ms    | 12.25ms       | 78MB        | 82        | 2.0x slower         |
| NUnit     | 4.2.0   | 1,890ms    | 9.45ms        | 62MB        | 106       | 1.6x slower         |
| MSTest    | 3.6.0   | 3,120ms    | 15.60ms       | 95MB        | 64        | 2.6x slower         |

## Detailed Results by Category

### Simple Tests (50 tests)
...

### Parameterized Tests (50 tests)
...

## Methodology
...
```

### Phase 5: GitHub Actions Integration (Priority 5)

**Workflow File**: `.github/workflows/speed-comparison.yml`

```yaml
name: Speed Comparison Benchmarks

on:
  workflow_dispatch:  # Manual trigger
  schedule:
    - cron: '0 0 * * 0'  # Weekly on Sunday at midnight UTC
  pull_request:
    paths:
      - 'src/**'
      - 'tools/speed-comparison/**'

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '10.0.x'
      
      - name: Run Speed Comparison
        run: |
          cd tools/speed-comparison
          dotnet run --project src/SpeedComparison.Runner \
            --configuration Release \
            --output results/latest.json
      
      - name: Update BENCHMARK_RESULTS.md
        run: |
          cd tools/speed-comparison
          # MarkdownGenerator updates BENCHMARK_RESULTS.md
      
      - name: Commit Results
        uses: stefanzweifel/git-auto-commit-action@v5
        with:
          commit_message: "Update speed comparison results [skip ci]"
          file_pattern: "tools/speed-comparison/results/*.md tools/speed-comparison/results/history/*.json"
```

## Technical Considerations

### Test Isolation
- Run each framework in a separate process to prevent interference
- Clear environment between runs
- Disable system effects (e.g., disk I/O, network)

### Measurement Accuracy
- Run multiple iterations (5+) and use median/average
- Warm up .NET runtime before measurements
- Use Release builds with optimizations
- Disable debugger attachment

### Framework Differences
- **NextUnit**: Uses Microsoft.Testing.Platform (`dotnet run`)
- **xUnit**: Uses `dotnet test` or xUnit console runner
- **NUnit**: Uses `dotnet test` or NUnit console runner
- **MSTest**: Uses `dotnet test`

Each framework may have different invocation methods, which will be handled by the `BenchmarkRunner`.

### Versioning
- Pin framework versions in test projects
- Document framework versions in results
- Test against latest stable versions
- Update regularly to track progress

## Success Criteria

1. **Fairness**: All frameworks run identical test logic
2. **Accuracy**: Results are reproducible within 5% variance
3. **Automation**: GitHub Actions automatically updates results
4. **Transparency**: Methodology is clearly documented
5. **Visibility**: Results are prominently displayed in README.md or dedicated file

## Timeline

1. **Phase 1** (Core Infrastructure): 2-3 hours
2. **Phase 2** (Test Projects): 2-3 hours
3. **Phase 3** (Benchmark Execution): 1-2 hours
4. **Phase 4** (Results & Reporting): 1-2 hours
5. **Phase 5** (GitHub Actions): 1 hour

**Total Estimated Time**: 7-11 hours of development work

## Future Enhancements

1. **More Frameworks**: Add Fixie, Expecto, BUnit comparisons
2. **Trend Analysis**: Chart performance over time
3. **Platform Comparison**: Windows vs. Linux vs. macOS
4. **Native AOT**: Compare Native AOT compilation times
5. **Real-world Scenarios**: Add integration test scenarios
6. **Memory Profiling**: Detailed allocation tracking with dotnet-trace

## References

- [TUnit Speed Comparison Tool](https://github.com/thomhurst/TUnit/tree/main/tools/speed-comparison)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [Microsoft.Testing.Platform](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform)
- [Process Performance Counters](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process)

---

**Status**: Planning phase complete, ready for implementation  
**Author**: GitHub Copilot Agent  
**Date**: 2025-12-10
