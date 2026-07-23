# Round-robin runtime comparison

Generated: 2026-07-23 12:43:45 UTC

Environment: Ubuntu 24.04.4 LTS (X64)

.NET SDK / Runtime: 10.0.302 / .NET 10.0.10

Processor: X64

Workload: 127 tests, 21 measured runs per participant

| Framework | Version | Runs | Mean | Median | StdDev | Min | Max | Median / NextUnit |
| --------- | ------- | ---: | ---: | -----: | -----: | --: | --: | ----------------: |
| NextUnit (AOT) | 1.15.1 | 21 | 21.89ms | 21.51ms | 0.77ms | 21.08ms | 24.19ms | 0.07x |
| TUnit (AOT) | 1.61.15 | 21 | 27.54ms | 27.45ms | 0.81ms | 26.21ms | 29.17ms | 0.09x |
| NextUnit | 1.15.1 | 21 | 310.43ms | 311.43ms | 4.19ms | 299.05ms | 317.01ms | 1.00x |
| MSTest | 4.3.2 | 21 | 439.89ms | 438.73ms | 8.34ms | 428.03ms | 468.41ms | 1.41x |
| NUnit | 4.6.1 | 21 | 513.87ms | 512.90ms | 4.74ms | 506.20ms | 522.76ms | 1.65x |
| xUnit | 3.2.2 | 21 | 550.79ms | 551.40ms | 8.22ms | 536.36ms | 565.35ms | 1.77x |
| TUnit | 1.61.15 | 21 | 555.09ms | 555.00ms | 3.41ms | 548.33ms | 561.28ms | 1.78x |

Method:

- Cyclic round-robin across five framework-dependent and two Native AOT executables, with one untimed warm-up per participant.
- Runtime samples exclude build and Native AOT publish time; both AOT executables target the host RID.
- Standalone MTP executables with stdout and stderr redirected.
- Common `--no-progress --no-ansi` arguments and telemetry disabled.
- TUnit HTML report generation disabled.
