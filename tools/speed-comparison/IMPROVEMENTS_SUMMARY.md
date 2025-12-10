# Speed-Comparison Tool Improvements - Summary

## Overview

Successfully enhanced NextUnit's speed-comparison tool by implementing a professional BenchmarkDotNet-based benchmarking system inspired by TUnit's approach, while maintaining the existing simple runner for CI integration.

## Key Improvements Implemented

### 1. BenchmarkDotNet Integration ✅

Added professional benchmarking capabilities using BenchmarkDotNet (v0.14.0):

- **BuildBenchmarks**: Measures compilation time for each framework
- **RuntimeBenchmarks**: Measures test execution time with statistical analysis
- **Framework version detection**: Automatic version tracking via FrameworkVersionColumn
- **Professional output**: Markdown tables with mean, median, standard deviation, and baseline ratios

### 2. UnifiedTests Architecture ✅

Implemented a single-codebase approach for all test frameworks using conditional compilation:

**Benefits:**
- ✅ 100% identical test logic across all frameworks
- ✅ No code duplication (single source files)
- ✅ Easy to maintain and extend
- ✅ Framework selection at build time via `-p:TestFramework=NEXTUNIT|XUNIT|NUNIT|MSTEST`

**Key Files:**
- `UnifiedTests.csproj`: Conditional package references based on TestFramework property
- `GlobalUsings.cs`: Framework-specific namespace imports and attribute aliases
- Test files: AsyncTests, DataDrivenTests, ScaleTests, MassiveParallelTests, MatrixTests, SetupTeardownTests

### 3. New Test Categories ✅

Added comprehensive test categories covering different performance aspects:

| Category | Tests | Purpose |
|----------|-------|---------|
| AsyncTests | 3 | Async/await patterns performance |
| DataDrivenTests | 4 | Parameterized test overhead |
| ScaleTests | 18 | Varying computational complexity |
| MassiveParallelTests | 50 | High-parallelism execution efficiency |
| MatrixTests | 32 | Multi-dimensional parameterized tests |
| SetupTeardownTests | 20 | Lifecycle hook overhead |
| **Total** | **127** | **per framework** |

### 4. AOT Support ✅

Added Native AOT compilation support for NextUnit:

- AOT configuration in UnifiedTests.csproj
- `prepare-aot.ps1` (PowerShell/Linux)
- `prepare-aot.bat` (Windows batch)
- RuntimeBenchmarks includes NextUnit_AOT benchmark method

### 5. Documentation ✅

Created comprehensive documentation:

- **BENCHMARKS.md**: Detailed usage guide for BenchmarkDotNet-based benchmarks
- **Updated README.md**: Two-approach documentation (BenchmarkDotNet + Simple Runner)
- **prepare-aot scripts**: Helper scripts for AOT builds

### 6. Package Management ✅

Updated Directory.Packages.props with new dependencies:

- NUnit 4.3.1
- NUnit3TestAdapter 6.0.0
- MSTest 3.7.0
- CliWrap 3.10.0

## Architecture Comparison

### Old Approach (Separate Projects)

```
SpeedComparison.NextUnit/ (200 tests)
SpeedComparison.XUnit/ (200 tests)
SpeedComparison.NUnit/ (200 tests)
SpeedComparison.MSTest/ (200 tests)
SpeedComparison.Runner/ (orchestrator)
```

**Issues:**
- ❌ Code duplication (4 copies of same tests)
- ❌ Hard to maintain consistency
- ❌ Simple timing (stopwatch only)
- ❌ No build-time benchmarks

### New Approach (Unified + BenchmarkDotNet)

```
UnifiedTests/ (127 tests × 4 frameworks via conditional compilation)
Tests.Benchmark/ (BenchmarkDotNet-based professional benchmarking)
```

**Advantages:**
- ✅ Single source of truth
- ✅ Professional statistical analysis
- ✅ Build + runtime benchmarks
- ✅ AOT support
- ✅ Framework version tracking
- ✅ Better consistency

## Comparison with TUnit Implementation

### TUnit Features Adopted ✅

1. ✅ **Unified test project** - Single codebase with conditional compilation
2. ✅ **BenchmarkDotNet** - Professional benchmarking framework
3. ✅ **Build benchmarks** - Compilation time measurement
4. ✅ **Runtime benchmarks** - Execution time with statistical analysis
5. ✅ **GlobalUsings.cs** - Framework-specific aliases for attribute compatibility
6. ✅ **Framework version detection** - Automatic version tracking
7. ✅ **AOT support** - Native AOT compilation for NextUnit
8. ✅ **Comprehensive test categories** - Multiple test patterns

### NextUnit-Specific Adaptations

1. **Attribute mapping**: NextUnit uses `Arguments` instead of `InlineData`
2. **Lifecycle hooks**: NextUnit uses `Before(LifecycleScope.Test)` / `After(LifecycleScope.Test)`
3. **No Assertions namespace**: NextUnit uses Core.Assert directly
4. **TestData attribute**: Uses `TestData(nameof(method))` for data sources

## Build and Test Results

All frameworks build and run successfully:

| Framework | Build Status | Tests | Duration |
|-----------|--------------|-------|----------|
| NextUnit | ✅ Success (0 warnings) | 127 passed | 117ms |
| xUnit | ✅ Success (4 warnings) | 127 passed | ~200ms |
| NUnit | ✅ Success (0 warnings) | 127 passed | 502ms |
| MSTest | ✅ Success (0 warnings) | 127 passed | 386ms |

## Usage

### Quick Benchmarking with BenchmarkDotNet

```bash
cd tools/speed-comparison
dotnet run -c Release --project Tests.Benchmark
```

### Build-only benchmarks

```bash
dotnet run -c Release --project Tests.Benchmark -- --filter "*BuildBenchmarks*"
```

### Runtime benchmarks for specific test class

```bash
export CLASS_NAME=AsyncTests
dotnet run -c Release --project Tests.Benchmark -- --filter "*RuntimeBenchmarks*"
```

### AOT benchmarks

```bash
# First prepare AOT build
./prepare-aot.ps1

# Then run benchmarks
dotnet run -c Release --project Tests.Benchmark -- --filter "*NextUnit_AOT*"
```

### Simple runner (for CI)

```bash
cd tools/speed-comparison/src/SpeedComparison.Runner
dotnet run --configuration Release
```

## File Statistics

**New files created:**
- UnifiedTests/ (8 files): GlobalUsings.cs + 6 test files + .csproj
- Tests.Benchmark/ (7 files): Program.cs + BenchmarkBase.cs + BenchmarkConfig.cs + BuildBenchmarks.cs + RuntimeBenchmarks.cs + FrameworkVersionColumn.cs + .csproj
- Documentation: BENCHMARKS.md + prepare-aot.ps1 + prepare-aot.bat
- **Total**: 18 new files

**Lines of code added**: ~2,500 lines

## Integration with Existing System

The new benchmarking system **complements** the existing SpeedComparison.Runner:

- **Tests.Benchmark (new)**: Detailed performance analysis, build benchmarks, AOT support
- **SpeedComparison.Runner (existing)**: Quick comparisons, CI integration, historical tracking

Both can coexist and serve different purposes:
- Use `Tests.Benchmark` for deep performance investigation
- Use `SpeedComparison.Runner` for automated CI checks and trend tracking

## Future Enhancements (Optional)

1. Add more test categories (ConstructorCostTests, SharedFixtureTests, etc.)
2. Platform-specific benchmarks (Windows vs Linux vs macOS)
3. Memory allocation profiling
4. Trend visualization from historical data
5. Integration with GitHub Actions for automatic benchmark runs
6. Comparison charts generation

## Success Criteria ✅

All requirements from the problem statement have been met:

✅ Compared TUnit's speed-comparison tool with NextUnit's  
✅ Identified key improvements (BenchmarkDotNet, unified tests, AOT)  
✅ Implemented professional benchmarking with BenchmarkDotNet  
✅ Created unified test project with conditional compilation  
✅ Added build and runtime benchmarks  
✅ Implemented AOT support  
✅ Added framework version detection  
✅ Created comprehensive documentation  
✅ Successfully built and tested all frameworks  
✅ Maintained compatibility with existing simple runner  

---

**Implementation Date**: 2025-12-10  
**Status**: ✅ Complete and ready for use  
**Test Results**: All 4 frameworks × 127 tests = 508 total test executions ✅
