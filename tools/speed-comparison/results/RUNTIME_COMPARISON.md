# Round-robin runtime comparison

Generated: 2026-07-21 13:21:16 UTC

Environment: Microsoft Windows 10.0.26200 (X64)

.NET SDK / Runtime: 10.0.302 / .NET 10.0.10

Processor: Intel64 Family 6 Model 191 Stepping 2, GenuineIntel

Workload: 127 tests, 20 measured runs per framework

| Framework | Version | Runs | Mean | Median | StdDev | Min | Max | Median / NextUnit |
| --------- | ------- | ---: | ---: | -----: | -----: | --: | --: | ----------------: |
| NextUnit | 1.15.0 | 20 | 540.67ms | 528.65ms | 48.10ms | 480.40ms | 661.66ms | 1.00x |
| MSTest | 4.3.2 | 20 | 620.70ms | 606.17ms | 48.14ms | 577.55ms | 795.72ms | 1.15x |
| xUnit | 3.2.2 | 20 | 687.21ms | 671.18ms | 40.51ms | 646.35ms | 788.54ms | 1.27x |
| NUnit | 4.6.1 | 20 | 723.55ms | 714.77ms | 52.43ms | 665.93ms | 900.46ms | 1.35x |
| TUnit | 1.61.15 | 20 | 745.03ms | 739.66ms | 38.03ms | 682.99ms | 814.13ms | 1.40x |

Method:

- Cyclic round-robin with one untimed warm-up per framework.
- Standalone MTP executables with stdout and stderr redirected.
- Common `--no-progress --no-ansi` arguments and telemetry disabled.
- TUnit HTML report generation disabled.
