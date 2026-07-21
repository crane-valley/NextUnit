# NextUnit Speed Comparison Benchmarks

The comparison suite covers NextUnit, TUnit, MSTest, xUnit, and NUnit with shared test bodies and
framework-native integration metadata.

## Reproduce the published result

```bash
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark -- --round-robin 20
```

Outputs:

- `results/RUNTIME_COMPARISON.md`: environment and summary statistics
- `results/runtime-comparison.json`: every accepted measurement with round and execution position

The runner rejects a framework if its process fails or does not report exactly 127 tests. It also
requires a round count divisible by five, which gives each framework the same number of runs in every
execution position.

## Measurement boundary

Each sample starts a prebuilt standalone test executable and stops after it exits. Wall-clock time
therefore includes:

- process and runtime startup;
- test discovery;
- framework scheduling and lifecycle hooks;
- execution of all 127 tests;
- result aggregation and console output generation.

Build and restore time are excluded from runtime samples. Output streams are consumed without being
printed during measurement.

## Common controls

- Release, JIT, .NET 10
- Native Microsoft.Testing.Platform executables for every framework
- One excluded warm-up process per framework
- Cyclic run order across NextUnit, TUnit, NUnit, MSTest, and xUnit
- `--no-progress --no-ansi` for every executable
- `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`
- `TUNIT_DISABLE_HTML_REPORTER=true`

TUnit's HTML report is disabled because it is an additional framework-specific file artifact. The
benchmark measures the common startup-to-results boundary; artifact generation should be benchmarked
separately when it matters to a workload.

## BenchmarkDotNet mode

Use BenchmarkDotNet when inspecting distributions or compilation cost:

```bash
# All JIT runtime benchmarks
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"

# Compilation benchmarks
dotnet run -c Release --project Tests.Benchmark -- --filter "*BuildBenchmarks*"
```

The configured MediumRun performs two launches with ten warm-ups and fifteen measured iterations per
launch. BenchmarkDotNet runs each framework as a separate benchmark, so time-varying machine load is
not balanced as directly as in round-robin mode.

## Native AOT

Native AOT is NextUnit-specific and excluded from the cross-framework JIT table. To include it in a
BenchmarkDotNet experiment:

```bash
export AUTOBUILD_AOT=true
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"
```

Use `$env:AUTOBUILD_AOT = "true"` in PowerShell.

## Interpretation limits

- This is a small, startup-heavy suite. It is not a steady-state execution-engine microbenchmark.
- Framework default concurrency is preserved rather than forced into a common scheduler.
- Equivalent framework-native metadata can have different implementation costs.
- Hardware, operating-system load, antivirus activity, and power management affect process timings.
- Feature coverage, IDE integration, ecosystem maturity, and migration effort are outside the
  performance measurement.

Do not use a single ratio as a universal framework ranking. Re-run the checked-in harness with a
representative test mix when making a project-specific decision.
