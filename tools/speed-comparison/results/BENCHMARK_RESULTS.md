# Speed Comparison Results

**Last Updated**: 2025-12-10 05:31:27 UTC
**Environment**: Ubuntu 24.04.3 LTS (X64)
**.NET Version**: 10.0.1
**Processor Count**: 4

## Summary

| Framework | Version | Total Time | Per-Test Time | Peak Memory | Tests/Sec | Relative Performance |
|-----------|---------|------------|---------------|-------------|-----------|---------------------|
| NextUnit  | 1.4.0   |     554ms |     2.77ms |     0.0MB |       361 | Baseline            |
| MSTest    | 3.7.0   |    1209ms |     6.04ms |     0.0MB |       165 | 2.2x slower         |
| NUnit     | 4.3.1   |    1256ms |     6.28ms |     0.0MB |       159 | 2.3x slower         |
| xUnit     | 2.9.3   |    1329ms |     6.64ms |     0.0MB |       150 | 2.4x slower         |

## Detailed Results

### NextUnit v1.4.0

- **Test Count**: 200
- **Passed**: 200
- **Failed**: 0
- **Skipped**: 0

**Timing:**
- Median: 553ms
- Average: 554ms
- Per-test: 2.77ms
- Throughput: 361 tests/second

**Memory:**
- Median peak: 0.0MB
- Average peak: 0.0MB

**Raw Data (all iterations):**
- Execution times: 552ms, 558ms, 546ms, 553ms, 564ms
- Peak memory: 0MB, 0MB, 0MB, 0MB, 0MB

### MSTest v3.7.0

- **Test Count**: 200
- **Passed**: 200
- **Failed**: 0
- **Skipped**: 0

**Timing:**
- Median: 1212ms
- Average: 1209ms
- Per-test: 6.04ms
- Throughput: 165 tests/second

**Memory:**
- Median peak: 0.0MB
- Average peak: 0.0MB

**Raw Data (all iterations):**
- Execution times: 1214ms, 1212ms, 1201ms, 1217ms, 1205ms
- Peak memory: 0MB, 0MB, 0MB, 0MB, 0MB

### NUnit v4.3.1

- **Test Count**: 200
- **Passed**: 200
- **Failed**: 0
- **Skipped**: 0

**Timing:**
- Median: 1259ms
- Average: 1256ms
- Per-test: 6.28ms
- Throughput: 159 tests/second

**Memory:**
- Median peak: 0.0MB
- Average peak: 0.0MB

**Raw Data (all iterations):**
- Execution times: 1265ms, 1271ms, 1259ms, 1246ms, 1243ms
- Peak memory: 0MB, 0MB, 0MB, 0MB, 0MB

### xUnit v2.9.3

- **Test Count**: 200
- **Passed**: 200
- **Failed**: 0
- **Skipped**: 0

**Timing:**
- Median: 1324ms
- Average: 1329ms
- Per-test: 6.64ms
- Throughput: 150 tests/second

**Memory:**
- Median peak: 0.0MB
- Average peak: 0.0MB

**Raw Data (all iterations):**
- Execution times: 1324ms, 1335ms, 1323ms, 1324ms, 1339ms
- Peak memory: 0MB, 0MB, 0MB, 0MB, 0MB

## Methodology

### Test Suite

Each framework runs an identical test suite containing **200 tests**:

- **50 Simple Tests**: Basic assertions (Equal, True, False)
- **50 Parameterized Tests**: Data-driven tests (5 methods × 10 parameters each)
- **25 Lifecycle Tests**: Tests with setup/teardown hooks
- **25 Async Tests**: Async/await test methods
- **25 Complex Assertion Tests**: Collection, string, and numeric assertions
- **25 Parallel Tests**: Tests designed to run concurrently

### Execution

- Each framework is run **5 times** in separate processes
- Median and average times are calculated from all iterations
- All projects built in **Release mode** with optimizations enabled
- Tests run using each framework's native test runner:
  - **NextUnit**: `dotnet run` (Microsoft.Testing.Platform)
  - **xUnit, NUnit, MSTest**: `dotnet test` (VSTest Platform)

### Metrics

- **Total Time**: Wall-clock time from process start to completion
- **Per-Test Time**: Total time ÷ test count
- **Peak Memory**: Maximum working set size during execution
- **Tests/Sec**: Test count ÷ (total time in seconds)
- **Relative Performance**: Compared to NextUnit baseline (1.0 = same speed)

### Fairness

All test implementations:
- Use identical test logic from `SpeedComparison.Shared`
- Follow framework best practices (native attributes and assertions)
- Include the same lifecycle hooks and async patterns
- Run with parallel execution enabled (where supported)
