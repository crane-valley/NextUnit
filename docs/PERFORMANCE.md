# Performance

NextUnit discovers tests at compile time and invokes generated delegates at runtime. The comparison
below measures complete test-process wall-clock time, including process startup, discovery,
execution, and result reporting. It is a startup-heavy workload, not an isolated assertion or
steady-state throughput microbenchmark.

## Cross-framework comparison

The checked-in unified suite compiles shared test bodies into 127 tests for NextUnit, TUnit, MSTest,
xUnit, and NUnit. NextUnit and TUnit are measured both as framework-dependent executables and as
self-contained Native AOT executables. Every executable must report all 127 tests as passed before a
measurement is accepted.

On Windows 11, .NET SDK 10.0.302 / runtime 10.0.10, and an Intel Core i5-13500, 21 measured runs per
participant produced:

| Framework | Version | Runs | Mean | Median | StdDev | Median / NextUnit |
| --------- | ------- | ---: | ---: | -----: | -----: | ----------------: |
| NextUnit (AOT) | current checkout (1.15.0) | 21 | 239.54ms | 223.38ms | 51.19ms | 0.51x |
| TUnit (AOT) | 1.61.15 | 21 | 243.18ms | 226.20ms | 46.15ms | 0.51x |
| NextUnit | current checkout (1.15.0) | 21 | 467.03ms | 442.31ms | 85.79ms | 1.00x |
| MSTest | 4.3.2 | 21 | 555.50ms | 528.43ms | 78.76ms | 1.19x |
| TUnit | 1.61.15 | 21 | 623.20ms | 580.56ms | 125.71ms | 1.31x |
| xUnit | 3.2.2 | 21 | 635.52ms | 593.86ms | 119.92ms | 1.34x |
| NUnit | 4.6.1 | 21 | 630.84ms | 604.33ms | 74.44ms | 1.37x |

Lower is better. The relative column divides each median by the framework-dependent NextUnit median;
it is not an AOT-only ratio. In this run, NextUnit (AOT) had a 1.2% lower median than TUnit (AOT),
which is small relative to the observed run-to-run variation and should be rechecked on other hosts.

## Methodology and anti-bias controls

- All seven participants target .NET 10 and run as native Microsoft.Testing.Platform executables.
  Five use Release framework-dependent builds; NextUnit (AOT) and TUnit (AOT) use Release Native AOT
  publishes for the same host runtime identifier. xUnit uses its v3 MTP v2 package rather than the
  older VSTest runner.
- Test bodies are shared. Attributes, data sources, and lifecycle hooks use each framework's native
  equivalent, so their integration overhead remains part of the result.
- One warm-up per participant is excluded. The 21 measured rounds use a cyclic order, making every
  participant occupy each of the seven execution positions exactly three times.
- Build, restore, and Native AOT publish time are excluded from runtime samples for every participant.
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
- Framework-dependent and Native AOT rows answer different deployment questions. Their difference
  includes runtime startup and packaging mode, so it must not be attributed only to framework code.
- NextUnit is the current checkout while competitors are pinned stable packages.
- Results are specific to one machine and one operating-system state. Treat the ratios as evidence for
  this workload, not a universal performance guarantee.
- Performance does not measure feature breadth, IDE behavior, ecosystem maturity, or migration cost.

## Reproducing results

Run the published round-robin comparison:

```bash
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark -- --round-robin 21
```

The round count must be a positive multiple of seven so each participant occupies every execution
position equally. The command builds five framework-dependent executables, publishes two Native AOT
executables, verifies 127 passing tests from each, writes the Markdown summary, and stores every raw
measurement as JSON.

BenchmarkDotNet remains available for within-framework distribution analysis and build benchmarks:

```bash
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"
```

Set `AUTOBUILD_AOT=true` to include Native AOT NextUnit and TUnit in BenchmarkDotNet diagnostic runs.
Round-robin mode always publishes and measures both AOT participants.
