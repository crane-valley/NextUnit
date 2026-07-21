# NextUnit Speed Comparison Tool

This tool compares the current NextUnit checkout with TUnit, MSTest, xUnit, and NUnit. One source
tree supplies shared test bodies while conditional aliases map attributes, data sources, and lifecycle
hooks to each framework's native API.

## Published cross-framework comparison

```bash
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark -- --round-robin 20
```

Round-robin mode is the source of the table in `docs/PERFORMANCE.md`. It:

1. Builds Release executables for all five frameworks.
2. Verifies that every executable passes all 127 tests.
3. Runs one excluded warm-up per framework.
4. Rotates the execution order for 20 measured rounds, so every framework occupies every position
   four times.
5. Writes a Markdown summary and raw per-round JSON data under `results/`.

The round count must be a positive multiple of five. The default is 20.

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

Set `AUTOBUILD_AOT=true` to include the NextUnit Native AOT benchmark. AOT publication may take
several minutes and is not included in the JIT comparison table.

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

- All frameworks target .NET 10 and build as standalone Microsoft.Testing.Platform executables.
- xUnit uses `xunit.v3.mtp-v2`; no compared framework uses the VSTest runner.
- Every measured process receives `--no-progress --no-ansi` with stdout and stderr redirected.
- Telemetry is disabled for all processes.
- TUnit HTML report generation is disabled so TUnit alone does not perform extra artifact I/O.

See [BENCHMARKS.md](BENCHMARKS.md) and [the performance methodology](../../docs/PERFORMANCE.md) for
the controls and limitations.
