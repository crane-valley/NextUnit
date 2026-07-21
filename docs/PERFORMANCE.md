# Performance

NextUnit discovers tests at compile time and invokes generated delegates at runtime. The comparison
below measures complete test-process wall-clock time, including process startup, discovery,
execution, and result reporting. It is a startup-heavy workload, not an isolated assertion or
steady-state throughput microbenchmark.

## Cross-framework comparison

The checked-in unified suite compiles shared test bodies into 127 tests for NextUnit, TUnit, MSTest,
xUnit, and NUnit. Every executable must report all 127 tests as passed before a measurement is
accepted.

On Windows 11, .NET SDK 10.0.302 / runtime 10.0.10, and an Intel Core i5-13500, 20 measured runs per
framework produced:

| Framework | Version | Runs | Mean | Median | StdDev | Median / NextUnit |
| --------- | ------- | ---: | ---: | -----: | -----: | ----------------: |
| NextUnit | current checkout (1.15.0) | 20 | 540.67ms | 528.65ms | 48.10ms | 1.00x |
| MSTest | 4.3.2 | 20 | 620.70ms | 606.17ms | 48.14ms | 1.15x |
| xUnit | 3.2.2 | 20 | 687.21ms | 671.18ms | 40.51ms | 1.27x |
| NUnit | 4.6.1 | 20 | 723.55ms | 714.77ms | 52.43ms | 1.35x |
| TUnit | 1.61.15 | 20 | 745.03ms | 739.66ms | 38.03ms | 1.40x |

Lower is better. The relative column divides each median by the NextUnit median.

## Methodology and anti-bias controls

- All five projects target .NET 10, use Release JIT builds, and run as native Microsoft.Testing.Platform
  executables. xUnit uses its v3 MTP v2 package rather than the older VSTest runner.
- Test bodies are shared. Attributes, data sources, and lifecycle hooks use each framework's native
  equivalent, so their integration overhead remains part of the result.
- One warm-up per framework is excluded. The 20 measured rounds use a cyclic order, making every
  framework run first, second, third, fourth, and fifth exactly four times.
- Standard output and error are redirected. Every executable receives `--no-progress --no-ansi`, and
  platform telemetry is disabled.
- TUnit's default HTML report is disabled because no other compared framework writes an HTML artifact
  during the run. Leaving that extra file I/O enabled would measure reporting policy rather than the
  common test lifecycle.
- Raw timings retain the round and execution position. No measured sample is manually removed.

See the [generated summary](../tools/speed-comparison/results/RUNTIME_COMPARISON.md) and
[raw measurements](../tools/speed-comparison/results/runtime-comparison.json).

## Limitations

- The suite contains many small tests, so process startup and discovery dominate. Larger or I/O-bound
  suites may rank differently.
- Default framework scheduling is intentionally preserved. This compares default package integration,
  not a forced common concurrency algorithm or isolated execution-engine primitive.
- NextUnit is the current checkout while competitors are pinned stable packages.
- Results are specific to one machine and one operating-system state. Treat the ratios as evidence for
  this workload, not a universal performance guarantee.
- Performance does not measure feature breadth, IDE behavior, ecosystem maturity, or migration cost.

## Reproducing results

Run the published round-robin comparison:

```bash
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark -- --round-robin 20
```

The round count must be a positive multiple of five so each framework occupies every execution
position equally. The command rebuilds all five executables, verifies 127 passing tests, writes the
Markdown summary, and stores every raw measurement as JSON.

BenchmarkDotNet remains available for within-framework distribution analysis and build benchmarks:

```bash
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"
```

Set `AUTOBUILD_AOT=true` to include Native AOT NextUnit in BenchmarkDotNet runs; AOT is intentionally
excluded from the cross-framework JIT table.
