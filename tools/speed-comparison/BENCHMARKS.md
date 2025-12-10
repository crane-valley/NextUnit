# NextUnit Speed Comparison Benchmarks

This directory contains realistic benchmarks comparing NextUnit, xUnit, NUnit, and MSTest frameworks using BenchmarkDotNet.

## Test Categories

Each framework implements the following test categories:

1. **AsyncTests** - Realistic async patterns using in-memory operations
2. **DataDrivenTests** - Parameterized tests using framework-specific data sources
3. **ScaleTests** - Tests with varying computational complexity
4. **MassiveParallelTests** - Tests demonstrating parallel execution patterns
5. **MatrixTests** - Multi-dimensional parameterized tests
6. **SetupTeardownTests** - Tests measuring setup/teardown hook overhead per test

## Running Benchmarks

### Quick Start (No manual build required):
```bash
# The benchmarks will automatically build missing test executables
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark
```

### For AOT Benchmarks:
AOT builds can take 5-10 minutes. You have two options:

Option 1: Build AOT manually before running benchmarks:
```bash
dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true
```

Option 2: Let the benchmark build AOT automatically (requires environment variable):
```bash
set AUTOBUILD_AOT=true  # Windows
export AUTOBUILD_AOT=true  # Linux/Mac
dotnet run -c Release --project Tests.Benchmark
```

### Run specific benchmark categories:
```bash
# Build benchmarks (measures compilation time)
dotnet run -c Release --project Tests.Benchmark -- --filter "*BuildBenchmarks*"

# Runtime benchmarks (runs all tests by default)
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"

# Runtime benchmarks for specific test class (optional)
# Note: CLASS_NAME filtering applies to NUnit, MSTest, and xUnit only.
# NextUnit runs all tests since it uses Microsoft.Testing.Platform which has different filtering mechanisms.
set CLASS_NAME=AsyncTests  # Windows
export CLASS_NAME=AsyncTests  # Linux/Mac
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"

# Available test class names for filtering:
# - AsyncTests
# - DataDrivenTests
# - ScaleTests
# - MassiveParallelTests
# - MatrixTests
# - SetupTeardownTests
```

### Run all benchmarks:
```bash
dotnet run -c Release --project Tests.Benchmark
```

## Benchmark Details

- NextUnit runs in both AOT (Ahead-of-Time) and regular JIT modes
- xUnit, NUnit, and MSTest use their native test runners
- All frameworks implement equivalent test logic
- Tests use realistic patterns without external dependencies
- In-memory operations simulate I/O without file system artifacts

## UnifiedTests Architecture

The UnifiedTests project uses conditional compilation to support all frameworks from a single codebase:

- **Single source files**: All test code shared across frameworks
- **Framework-specific attributes**: Using preprocessor directives
- **GlobalUsings.cs**: Framework-specific namespace imports and attribute aliases
- **Build-time framework selection**: Via `-p:TestFramework=NEXTUNIT|XUNIT|NUNIT|MSTEST`

This ensures:
✅ **100% identical test logic** across all frameworks  
✅ **No code duplication** - easier to maintain  
✅ **Fair comparisons** - same operations, same allocations  
✅ **Easy to add new tests** - write once, run on all frameworks  

## AOT Support

NextUnit supports Native AOT compilation for improved startup and memory performance:

```bash
# Build NextUnit with AOT
dotnet publish UnifiedTests/UnifiedTests.csproj -c Release -p:TestFramework=NEXTUNIT -p:PublishAot=true
```

Or use the provided script:
```bash
# Windows
./prepare-aot.bat

# PowerShell/Linux
./prepare-aot.ps1
```

## Results

Benchmark results are output as Markdown files in the BenchmarkDotNet.Artifacts directory.

## Differences from Old Approach

The new unified approach provides several advantages over the previous separate-project approach:

1. **Better consistency** - All frameworks use exactly the same test code
2. **Easier maintenance** - Single file to update instead of 4 copies
3. **Professional benchmarking** - BenchmarkDotNet provides more accurate measurements
4. **Build-time benchmarks** - Can measure compilation overhead
5. **AOT support** - Can benchmark Native AOT compilation
6. **Framework version tracking** - Automatic version detection and display

## Integration with Existing Tools

This new benchmarking system complements the existing SpeedComparison.Runner:

- **Tests.Benchmark (new)**: Professional BenchmarkDotNet-based measurements for detailed analysis
- **SpeedComparison.Runner (existing)**: Simpler runner for quick comparisons and CI integration

Both can coexist and serve different purposes:
- Use Tests.Benchmark for detailed performance analysis
- Use SpeedComparison.Runner for automated CI checks

---

**Last Updated**: 2025-12-10  
**NextUnit Version**: 1.4.0
