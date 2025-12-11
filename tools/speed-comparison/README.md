# NextUnit Speed Comparison Tool

This tool benchmarks NextUnit against other popular .NET test frameworks (xUnit, NUnit, MSTest) using identical test cases via conditional compilation to provide fair performance comparisons.

## BenchmarkDotNet-based Benchmarking ⭐

For detailed performance analysis using industry-standard BenchmarkDotNet:

```bash
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark
```

Features:
- ✅ **Build benchmarks** - Measures compilation time
- ✅ **Runtime benchmarks** - Measures execution time
- ✅ **AOT support** - Tests Native AOT compilation
- ✅ **Statistical analysis** - Mean, median, standard deviation
- ✅ **Framework version detection** - Automatic version tracking
- ✅ **Professional output** - Markdown tables with detailed metrics

See [BENCHMARKS.md](BENCHMARKS.md) for detailed documentation.

## UnifiedTests Architecture

The UnifiedTests project uses conditional compilation to support all frameworks from a single codebase:

- **Single source files**: All test code shared across frameworks
- **Framework-specific attributes**: Using preprocessor directives
- **GlobalUsings.cs**: Framework-specific namespace imports
- **Build-time framework selection**: Via `-p:TestFramework=NEXTUNIT|XUNIT|NUNIT|MSTEST`

This ensures 100% identical test logic across all frameworks.

## Quick Start

### Running Benchmarks Locally

```bash
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark
```

The tool will:
1. **Automatically build** missing test executables for all frameworks
2. Run benchmarks for build time and runtime performance
3. Collect detailed statistical metrics
4. Generate professional BenchmarkDotNet reports

**Note**: The NextUnit_AOT benchmark requires a published AOT build. If not found, it will be skipped unless you:
- Run `dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true` manually, OR
- Set environment variable `AUTOBUILD_AOT=true` to build it automatically (takes 5-10 minutes)

### Viewing Results

Results are displayed in the console with markdown tables and saved to the BenchmarkDotNet artifacts directory.

## Test Suite

The UnifiedTests project contains **127 tests per framework**:

| Category | Count | Description |
|----------|-------|-------------|
| Async Tests | 3 | Async/await patterns performance |
| Data-Driven Tests | 4 | Parameterized test overhead |
| Scale Tests | 18 | Varying computational complexity |
| Massive Parallel Tests | 50 | High-parallelism execution efficiency |
| Matrix Tests | 32 | Multi-dimensional parameterized tests |
| Setup/Teardown Tests | 20 | Lifecycle hook overhead |

All tests use conditional compilation to ensure identical logic across frameworks.

## Metrics Collected

### Build Time Metrics
- **Compilation time** for each framework configuration
- **Comparison across frameworks** showing relative build performance

### Runtime Metrics
- **Test execution time** with statistical analysis
- **Mean, median, standard deviation** across multiple iterations
- **Baseline comparisons** showing relative performance

### Native AOT
- **NextUnit-specific benchmarks** for Native AOT compilation
- **Startup time and memory** comparisons

## Architecture

### Projects

```
tools/speed-comparison/
├── UnifiedTests/                     # Single codebase for all frameworks
│   ├── *.cs                          # Test files with conditional compilation
│   ├── GlobalUsings.cs               # Framework-specific imports
│   └── UnifiedTests.csproj           # Conditional package references
├── Tests.Benchmark/                  # BenchmarkDotNet orchestrator
│   ├── BuildBenchmarks.cs            # Compilation time benchmarks
│   ├── RuntimeBenchmarks.cs          # Execution time benchmarks
│   └── Tests.Benchmark.csproj
└── results/
    └── BenchmarkDotNet.Artifacts/    # Generated benchmark results
```

### How It Works

1. **Build Phase**: UnifiedTests is built with different TestFramework properties
2. **Execution Phase**: BenchmarkDotNet runs each configuration multiple times
3. **Measurement**: 
   - Build time using MSBuild API
   - Runtime using BenchmarkDotNet's precise timing
4. **Analysis**: Statistical analysis with mean, median, std dev
5. **Reporting**: Professional markdown tables and charts

## Test Framework Execution

Each framework is benchmarked using its native patterns:

- **NextUnit**: Direct execution with Microsoft.Testing.Platform
- **xUnit**: VSTest Platform runner
- **NUnit**: VSTest Platform runner  
- **MSTest**: VSTest Platform runner

This reflects real-world usage patterns for each framework.

## Fairness Guarantees

✅ **Identical test logic** - All frameworks use conditional compilation from UnifiedTests  
✅ **Same test count** - 127 tests per framework  
✅ **Release builds** - Optimizations enabled for all  
✅ **Statistical rigor** - BenchmarkDotNet's professional analysis  
✅ **Multiple iterations** - Automated by BenchmarkDotNet  
✅ **Framework-native patterns** - Using each framework's best practices  

## Limitations

⚠️ **Not measured**:
- IDE integration performance
- Test authoring ergonomics
- Ecosystem maturity

⚠️ **Context-dependent**:
- Results vary by hardware and system load
- Build time affected by cache state
- AOT support differs by framework

**Performance is one factor among many.** Consider features, ecosystem, team familiarity, and project requirements when choosing a test framework.

## GitHub Actions Integration

The benchmarks run automatically on:
- **Manual trigger** (workflow_dispatch)
- **Weekly schedule** (Sunday at midnight UTC)
- **Pull requests** affecting source code

Results are displayed in workflow outputs and can be committed to the repository.

## Development

### Adding New Test Cases

Edit the test files in `UnifiedTests/` and use conditional compilation:

```csharp
#if NEXTUNIT
[Test]
#elif XUNIT
[Fact]
#elif NUNIT
[Test]
#elif MSTEST
[TestMethod]
#endif
public void MyTest()
{
    // Shared test logic
}
```

### Customizing Benchmarks

Edit `Tests.Benchmark/BuildBenchmarks.cs` or `RuntimeBenchmarks.cs` to add new benchmark configurations.

## Troubleshooting

**Build failures**: Ensure .NET 10 SDK is installed  
**Missing results**: Check that UnifiedTests builds for all framework configurations  
**Inconsistent results**: BenchmarkDotNet handles warmup and multiple iterations automatically  

## References

- Inspired by [TUnit Speed Comparison Tool](https://github.com/thomhurst/TUnit/tree/main/tools/speed-comparison)
- Uses [BenchmarkDotNet](https://benchmarkdotnet.org/) for professional benchmarking
- Built on [Microsoft.Testing.Platform](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform)

---

**Last Updated**: 2025-12-10  
**NextUnit Version**: 1.5.0
