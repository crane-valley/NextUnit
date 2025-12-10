# NextUnit Speed Comparison Tool

This tool benchmarks NextUnit against other popular .NET test frameworks (xUnit, NUnit, MSTest) using identical test cases to provide fair performance comparisons.

## Quick Start

### Running Benchmarks Locally

```bash
cd tools/speed-comparison/src/SpeedComparison.Runner
dotnet run --configuration Release
```

The tool will:
1. Build all test projects in Release mode
2. Run each framework's test suite 5 times
3. Collect timing and memory metrics
4. Generate a markdown report
5. Save results to `results/BENCHMARK_RESULTS.md`

### Viewing Results

After running, check:
- **Latest results**: `tools/speed-comparison/results/BENCHMARK_RESULTS.md`
- **Historical data**: `tools/speed-comparison/results/history/`

## Test Suite

Each framework runs **200 identical tests**:

| Category | Count | Description |
|----------|-------|-------------|
| Simple Tests | 50 | Basic assertions (Equal, True, False) |
| Parameterized Tests | 50 | Data-driven tests (5 methods × 10 parameters) |
| Lifecycle Tests | 25 | Tests with setup/teardown hooks |
| Async Tests | 25 | Async/await test methods |
| Complex Assertions | 25 | Collection, string, numeric assertions |
| Parallel Tests | 25 | Tests designed for concurrent execution |

All tests use shared logic from `SpeedComparison.Shared` to ensure fairness.

## Metrics Collected

### Timing Metrics
- **Total execution time** (wall-clock time)
- **Median and average** across 5 iterations
- **Per-test overhead** (total time ÷ test count)
- **Throughput** (tests per second)

### Memory Metrics
- **Peak working set** (maximum memory usage)
- **Median and average** across iterations

### Relative Performance
- Comparison to NextUnit baseline (1.0 = same, 2.0 = 2x slower, 0.5 = 2x faster)

## Architecture

### Projects

```
tools/speed-comparison/
├── src/
│   ├── SpeedComparison.Shared/       # Common test data and operations
│   ├── SpeedComparison.NextUnit/     # NextUnit test implementation
│   ├── SpeedComparison.XUnit/        # xUnit test implementation
│   ├── SpeedComparison.NUnit/        # NUnit test implementation
│   ├── SpeedComparison.MSTest/       # MSTest test implementation
│   └── SpeedComparison.Runner/       # Benchmark orchestrator
└── results/
    ├── BENCHMARK_RESULTS.md          # Latest results (auto-updated)
    └── history/                      # Historical JSON results
```

### How It Works

1. **Build Phase**: All test projects are built in Release mode
2. **Execution Phase**: Each framework runs in a separate process, 5 times
3. **Measurement**: 
   - `Stopwatch` for wall-clock timing
   - `Process.PeakWorkingSet64` for memory usage
4. **Analysis**: Median/average calculated, relative performance computed
5. **Reporting**: Generate markdown and JSON outputs

## Test Runner Differences

Each framework uses its native test runner:

- **NextUnit**: `dotnet run` (Microsoft.Testing.Platform)
- **xUnit**: `dotnet test` (VSTest Platform)
- **NUnit**: `dotnet test` (VSTest Platform)
- **MSTest**: `dotnet test` (VSTest Platform)

This reflects real-world usage patterns for each framework.

## Fairness Guarantees

✅ **Identical test logic** - All frameworks use shared test operations  
✅ **Same test count** - 200 tests per framework  
✅ **Release builds** - Optimizations enabled for all  
✅ **Multiple iterations** - 5 runs per framework to reduce variance  
✅ **Process isolation** - Each framework runs in a clean process  
✅ **Framework-native patterns** - Using each framework's best practices  

## Limitations

⚠️ **Not measured**:
- Test discovery time (initial startup overhead)
- IDE integration performance
- Build-time code generation overhead
- Native AOT compilation support
- Test authoring ergonomics

⚠️ **Measured but context-dependent**:
- Memory usage (affected by test complexity)
- Execution time (affected by system load)

**Performance is one factor among many.** Consider features, ecosystem, team familiarity, and project requirements when choosing a test framework.

## GitHub Actions Integration

The benchmarks run automatically on:
- **Manual trigger** (workflow_dispatch)
- **Weekly schedule** (Sunday at midnight UTC)
- **Pull requests** affecting source code

Results are automatically committed to the repository.

## Development

### Adding New Test Frameworks

1. Create a new test project in `src/SpeedComparison.YourFramework/`
2. Implement 200 tests using framework-native patterns
3. Add benchmark runner support in `BenchmarkRunner.cs`
4. Update version numbers in framework metrics

### Modifying Test Cases

Edit `SpeedComparison.Shared/TestOperations.cs` and `SharedTestData.cs` to change shared test logic. Then update all framework implementations to match.

### Customizing Reports

Edit `MarkdownGenerator.cs` to change report format or add new metrics.

## Troubleshooting

**Build failures**: Ensure .NET 10 SDK is installed  
**Missing results**: Check that all test projects build successfully  
**Inconsistent results**: Run multiple times; first run may be slower due to JIT  
**Memory issues**: Peak memory includes framework overhead, not just test execution  

## References

- Inspired by [TUnit Speed Comparison Tool](https://github.com/thomhurst/TUnit/tree/main/tools/speed-comparison)
- Uses [BenchmarkDotNet](https://benchmarkdotnet.org/) concepts
- Built on [Microsoft.Testing.Platform](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform)

---

**Last Updated**: 2025-12-10  
**NextUnit Version**: 1.4.0
