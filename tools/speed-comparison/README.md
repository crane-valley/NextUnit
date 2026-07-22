# NextUnit Speed Comparison Tool

This tool compares the current NextUnit checkout with TUnit, MSTest, xUnit, and NUnit. One source
tree supplies shared test bodies while conditional aliases map attributes, data sources, and lifecycle
hooks to each framework's native API.

## Published cross-framework comparison

```bash
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark -- --round-robin 21
```

Round-robin mode is the source of the table in `docs/PERFORMANCE.md`. It:

1. Builds Release framework-dependent executables for all five frameworks and publishes matching
   Native AOT executables for NextUnit and TUnit.
2. Verifies that all seven executables pass all 127 tests.
3. Runs one excluded warm-up per participant.
4. Rotates the execution order for 21 measured rounds, so every participant occupies every position
   three times.
5. Writes a Markdown summary and raw per-round JSON data under `results/`.

The round count must be a positive multiple of seven. The default is 21.

## BenchmarkDotNet diagnostics

BenchmarkDotNet is retained for build measurements, Native AOT experiments, and detailed
within-framework distributions:

```bash
# Runtime distributions
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"

# Build benchmarks
dotnet run -c Release --project Tests.Benchmark -- --filter "*BuildBenchmarks*"
```

The checked-in MediumRun job uses two launches, ten warm-ups per launch, and fifteen measured
iterations per launch. Frameworks are benchmarked sequentially in this mode, so use round-robin mode
for the published cross-framework ranking.

Set `AUTOBUILD_AOT=true` to include the NextUnit and TUnit Native AOT benchmarks in BenchmarkDotNet
diagnostics. The published round-robin comparison always includes both AOT participants.

## Unified suite

The suite produces 127 tests per framework:

| Category | Count | Description |
| -------- | ----: | ----------- |
| Async tests | 3 | Async/await patterns |
| Data-driven tests | 4 | Parameterization overhead |
| Scale tests | 18 | Varying computational work |
| Massive parallel tests | 50 | Default scheduling behavior |
| Matrix tests | 32 | Multi-dimensional parameterization |
| Setup/teardown operations | 20 | Per-test lifecycle hooks |

The test bodies are shared, but framework integration code cannot be byte-for-byte identical. Each
framework uses its own supported attributes, data representation, lifecycle model, discovery, and
scheduler. Those differences are part of the measured default integration.

## Runner normalization

- All participants target .NET 10 and run as standalone Microsoft.Testing.Platform executables.
- NextUnit and TUnit are measured as both framework-dependent and same-RID Native AOT executables.
- xUnit uses `xunit.v3.mtp-v2`; no compared framework uses the VSTest runner.
- Every measured process receives `--no-progress --no-ansi` with stdout and stderr redirected.
- Telemetry is disabled for all processes.
- TUnit HTML report generation is disabled so TUnit alone does not perform extra artifact I/O.

See [BENCHMARKS.md](BENCHMARKS.md) and [the performance methodology](../../docs/PERFORMANCE.md) for
the controls and limitations.
