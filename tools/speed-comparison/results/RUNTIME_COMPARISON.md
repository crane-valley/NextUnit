# Round-robin runtime comparison

Generated: 2026-07-22 01:30:04 UTC

Environment: Microsoft Windows 10.0.26200 (X64)

.NET SDK / Runtime: 10.0.302 / .NET 10.0.10

Processor: Intel64 Family 6 Model 191 Stepping 2, GenuineIntel

Workload: 127 tests, 21 measured runs per participant

| Framework | Version | Runs | Mean | Median | StdDev | Min | Max | Median / NextUnit |
| --------- | ------- | ---: | ---: | -----: | -----: | --: | --: | ----------------: |
| NextUnit (AOT) | 1.15.0 | 21 | 239.54ms | 223.38ms | 51.19ms | 199.25ms | 419.77ms | 0.51x |
| TUnit (AOT) | 1.61.15 | 21 | 243.18ms | 226.20ms | 46.15ms | 194.85ms | 380.59ms | 0.51x |
| NextUnit | 1.15.0 | 21 | 467.03ms | 442.31ms | 85.79ms | 404.11ms | 750.65ms | 1.00x |
| MSTest | 4.3.2 | 21 | 555.50ms | 528.43ms | 78.76ms | 503.83ms | 802.57ms | 1.19x |
| TUnit | 1.61.15 | 21 | 623.20ms | 580.56ms | 125.71ms | 525.58ms | 1035.13ms | 1.31x |
| xUnit | 3.2.2 | 21 | 635.52ms | 593.86ms | 119.92ms | 532.90ms | 1034.94ms | 1.34x |
| NUnit | 4.6.1 | 21 | 630.84ms | 604.33ms | 74.44ms | 553.11ms | 856.49ms | 1.37x |

Method:

- Cyclic round-robin across five framework-dependent and two Native AOT executables, with one untimed warm-up per participant.
- Runtime samples exclude build and Native AOT publish time; both AOT executables target the host RID.
- Standalone MTP executables with stdout and stderr redirected.
- Common `--no-progress --no-ansi` arguments and telemetry disabled.
- TUnit HTML report generation disabled.
