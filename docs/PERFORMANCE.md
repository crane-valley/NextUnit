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

On the Ubuntu 24.04 GitHub Actions runner, with .NET SDK 10.0.302 / runtime 10.0.10, 21 measured
runs per participant produced:

| Framework | Version | Runs | Mean | Median | StdDev | Median / NextUnit |
| --------- | ------- | ---: | ---: | -----: | -----: | ----------------: |
| NextUnit (AOT) | PR #160 checkout (1.15.1 assembly) | 21 | 21.89ms | 21.51ms | 0.77ms | 0.07x |
| TUnit (AOT) | 1.61.15 | 21 | 27.54ms | 27.45ms | 0.81ms | 0.09x |
| NextUnit | PR #160 checkout (1.15.1 assembly) | 21 | 310.43ms | 311.43ms | 4.19ms | 1.00x |
| MSTest | 4.3.2 | 21 | 439.89ms | 438.73ms | 8.34ms | 1.41x |
| NUnit | 4.6.1 | 21 | 513.87ms | 512.90ms | 4.74ms | 1.65x |
| xUnit | 3.2.2 | 21 | 550.79ms | 551.40ms | 8.22ms | 1.77x |
| TUnit | 1.61.15 | 21 | 555.09ms | 555.00ms | 3.41ms | 1.78x |

Lower is better. The relative column divides each median by the framework-dependent NextUnit median;
it is not an AOT-only ratio. In this run, NextUnit had a 43.9% lower framework-dependent median than
TUnit, while NextUnit (AOT) had a 21.6% lower median than TUnit (AOT). These are results for this
runner and workload, not universal speed-up claims.

This snapshot came from the
[PR #160 speed-comparison run](https://github.com/crane-valley/NextUnit/actions/runs/30007960214)
at commit `7d251a3`. The checked-in generated summary and raw measurements come from the same run.

## PR #160 within-framework comparison

PR #160 changed generated invocation, execution-state allocation, analyzer caching, VSTest lookup,
and scheduler batching. To isolate those changes from cross-framework and host differences, the
same BenchmarkDotNet harness was applied to the pre-change commit `f2dacac` and PR head `7d251a3`,
then run sequentially on Windows 11, .NET SDK 10.0.302 / runtime 10.0.10, and an Intel Core
i5-13500:

| Benchmark | Scale | Before (`f2dacac`) | PR #160 (`7d251a3`) | Change |
| --------- | ----: | -----------------: | -------------------: | -----: |
| Scheduler batch creation | 100 tests | 25.815us | 19.247us | 25.4% faster |
| Scheduler batch creation | 1,000 tests | 264.878us | 208.315us | 21.4% faster |
| Scheduler batch creation | 10,000 tests | 6.906ms / 27,629,003 B | 3.372ms / ~2,387,517 B | 51.2% faster / 91.4% less allocation |
| Sample-suite process execution | 376 tests | 2.578s | 2.119s | 17.8% faster |

The scheduler measurements used three warm-ups and three measured iterations. The sample-suite
benchmark used three warm-ups and ten configured iterations; BenchmarkDotNet excluded one PR-side
outlier. The sample-suite timing includes process startup and test execution. Its allocation
diagnostic covers only the benchmark host process, not the child test process, so no allocation
comparison is reported for that row.

## Deferred data source enumeration

`[TestData(..., DeferredEnumeration = true)]` moves row enumeration from discovery to execution. The
`DataSourceExpansionBenchmarks` harness measures the three pieces separately on Windows 11, .NET SDK
10.0.302 / runtime 10.0.10, and an Intel Core i5-13500, with ten measured iterations per case:

| Benchmark | 100 rows | 1,000 rows | 10,000 rows | Allocated at 10,000 rows |
| --------- | -------: | ---------: | ----------: | -----------------------: |
| Discovery: eager expansion of every row | 69.550us | 608.750us | 5.767ms | 9,407.88 KB |
| Discovery: deferred placeholder only | 8.600us | 11.300us | 18.050us | 1.3 KB |
| Execution: deferred fan-out into rows | 66.500us | 589.750us | 5.982ms | 9,407.74 KB |
| Execution: deferred async fan-out into rows | 71.550us | 680.800us | 6.408ms | 9,642.30 KB |

Medians are reported. These operations run in microseconds, so a single JIT warm-up iteration
dominates the arithmetic mean; the medians above are the representative figures, and the means are
kept out for that reason rather than because they were unflattering.

Two properties are what the benchmark exists to prove. Deferred discovery is flat in both time and
allocation: 1.3 KB and under twenty microseconds whether the source can produce 100 rows or 10,000,
because the member is never called. And deferral moves work rather than multiplying it -- the
execution fan-out costs what eager discovery would have cost, allocating 9,407.74 KB against eager's
9,407.88 KB at 10,000 rows, so neither path builds an intermediate collection on top of the rows
themselves.

The asynchronous fan-out is measured separately because it is the path where a second such collection
could appear: its rows arrive one at a time through an enumerator rather than as a ready collection,
so it projects each row into a test case as it arrives instead of materializing every row first. Its
2.5% allocation overhead at 10,000 rows is the async enumerator machinery -- roughly 24 bytes per row
-- not a second copy of the source.

## Cross-framework methodology and anti-bias controls

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

Run the scheduler and complete sample-suite benchmarks used for within-framework regression checks:

```bash
dotnet run -c Release --project benchmarks/NextUnit.Benchmarks/NextUnit.Benchmarks.csproj -- \
  --filter "*ParallelSchedulerBenchmarks*" --job short
dotnet run -c Release --no-build --project benchmarks/NextUnit.Benchmarks/NextUnit.Benchmarks.csproj -- \
  --filter "*TestSuiteExecutionBenchmarks*"
```

BenchmarkDotNet also remains available for cross-framework distribution analysis and build
benchmarks:

```bash
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"
```

Set `AUTOBUILD_AOT=true` to include Native AOT NextUnit and TUnit in BenchmarkDotNet diagnostic runs.
Round-robin mode always publishes and measures both AOT participants.
