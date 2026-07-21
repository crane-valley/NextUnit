# Performance

NextUnit discovers tests at compile time and invokes generated delegates at runtime. Performance
claims are validated against executable test projects so measurements include startup, discovery,
execution, and result reporting rather than isolated assertion microbenchmarks.

## TUnit comparison

The checked-in unified suite contains 127 equivalent tests covering async work, parameterized data,
scale, parallel execution, matrix-style combinations, and per-test setup/teardown.

On Windows 11, .NET 10.0.10, and an Intel Core i5-13500, 10 timed process runs after one warm-up
produced:

| Framework | Version | Mean | Median | Relative to NextUnit |
| --------- | ------- | ---: | -----: | -------------------: |
| NextUnit | current checkout (1.15.0) | 424.62ms | 423.60ms | 1.00x |
| TUnit | 1.61.15 | 1,085.71ms | 1,086.44ms | 2.57x slower |

These are default-package JIT results, not a claim that every workload or environment has the same
ratio. TUnit provides a broader feature set, while NextUnit intentionally keeps a smaller runtime.

## Reproducing results

```bash
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"
```

The benchmark project builds missing framework executables automatically. Set `AUTOBUILD_AOT=true`
to include Native AOT NextUnit; AOT publication can take several minutes.

See the [recorded environment and methodology](../tools/speed-comparison/results/BENCHMARK_RESULTS.md)
and the [benchmark guide](../tools/speed-comparison/BENCHMARKS.md).
