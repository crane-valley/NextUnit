# NextUnit

[![.NET](https://github.com/kiyoaki/NextUnit/actions/workflows/dotnet.yml/badge.svg)](https://github.com/kiyoaki/NextUnit/actions/workflows/dotnet.yml)

A modern, high-performance test framework for .NET 10+ that combines TUnit's architecture with xUnit's familiar assertions.

## Vision

NextUnit bridges the gap between modern testing infrastructure and developer-friendly APIs:
- **TUnit's modern architecture** - Microsoft.Testing.Platform integration, Native AOT support, source generators
- **xUnit's ergonomic assertions** - Classic `Assert.Equal(expected, actual)`, no fluent syntax, synchronous by default

## Features

### Implemented (v0.1-alpha)
- ✅ **Clear attribute naming** - `[Test]`, `[Before]`, `[After]` (not `[Fact]` or `[Theory]`)
- ✅ **Classic assertions** - `Assert.Equal`, `Assert.True`, `Assert.Throws` (familiar to xUnit/NUnit/MSTest users)
- ✅ **Lifecycle hooks** - `[Before(LifecycleScope.Test)]`, `[After(LifecycleScope.Test)]`
- ✅ **Dependency ordering** - `[DependsOn(nameof(OtherTest))]` ensures execution order
- ✅ **Parallel control** - `[NotInParallel]`, `[ParallelLimit(4)]` for fine-grained concurrency
- ✅ **Instance-per-test** - Each test gets a fresh class instance (maximizes isolation)
- ✅ **Async support** - `async Task` tests, `Assert.ThrowsAsync<T>` for async assertions
- ✅ **Proper disposal** - Automatic `IDisposable`/`IAsyncDisposable` cleanup
- ✅ **Source generator** - Emits test registry with zero-reflection delegates (M1 - Complete)
- ✅ **Generator diagnostics** - Detects dependency cycles and unresolved dependencies
- ✅ **Zero-reflection execution** - Test methods invoked via delegates, not reflection

### Planned (see [PLANS.md](PLANS.md))
- 📋 **Parameterized tests** - `[Arguments]` and `[TestData]` attributes (M1.5)
- 📋 **Advanced lifecycle** - Assembly/Class/Session scopes (M2)
- 📋 **Smart scheduler** - Parallel execution with constraint enforcement (M3)
- 📋 **Rich assertions** - Collections, strings, numerics with great error messages (M5)
- 📋 **Full Native AOT** - Eliminate remaining type discovery reflection (Future optimization)

## Quick Start

### Installation

```bash
# Coming soon to NuGet
# For now, build from source
dotnet build
```

### Running Tests

NextUnit uses **Microsoft.Testing.Platform** for test execution. To run tests:

```bash
# Run all tests in a project
dotnet run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj

# Run with specific options
dotnet run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj -- --help

# Run with minimum expected tests check
dotnet run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj -- --minimum-expected-tests 20

# Generate test results
dotnet run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj -- --results-directory ./TestResults --report-trx
```

**Note**: Unlike traditional test frameworks, NextUnit does **not** use `dotnet test`. Tests are executed as a console application using Microsoft.Testing.Platform.

### Writing Tests

```csharp
using NextUnit;

public class CalculatorTests
{
    [Test]
    public void Addition_Works()
    {
        var result = 2 + 2;
        Assert.Equal(4, result);
    }

    [Test]
    public async Task AsyncOperation_Succeeds()
    {
        var result = await GetValueAsync();
        Assert.NotNull(result);
    }

    [Test]
    public void Division_ThrowsOnZero()
    {
        var ex = Assert.Throws<DivideByZeroException>(() => 
        {
            var x = 1 / 0;
        });
    }
}
```

### Lifecycle Hooks

```csharp
public class DatabaseTests
{
    Database? _db;

    [Before(LifecycleScope.Test)]
    public void Setup()
    {
        _db = new Database();
    }

    [After(LifecycleScope.Test)]
    public void Cleanup()
    {
        _db?.Dispose();
    }

    [Test]
    public void CanInsertRecord()
    {
        _db!.Insert(new Record());
        Assert.Equal(1, _db.Count);
    }
}
```

### Test Dependencies

```csharp
public class IntegrationTests
{
    [Test]
    public void Step1_Initialize()
    {
        // Setup code
    }

    [Test]
    [DependsOn(nameof(Step1_Initialize))]
    public void Step2_Process()
    {
        // This runs after Step1_Initialize completes
    }

    [Test]
    [DependsOn(nameof(Step1_Initialize), nameof(Step2_Process))]
    public void Step3_Verify()
    {
        // This runs after both previous tests complete
    }
}
```

### Parallel Control

```csharp
// Runs in parallel with other tests (default)
public class FastTests
{
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }
}

// Runs serially (one at a time)
[NotInParallel]
public class SlowTests
{
    [Test]
    public void DatabaseTest() { }

    [Test]
    public void FileSystemTest() { }
}

// Limits parallelism to 2 concurrent tests
[ParallelLimit(2)]
public class ModerateTests
{
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }

    [Test]
    public void Test3() { }
}
```

## Architecture

NextUnit is designed for **performance** and **maintainability**:

### Zero-Reflection Execution ✅
- ✅ No `System.Reflection` in test execution paths
- ✅ Source generator produces delegate-based test registry
- ✅ Fast startup (<2ms discovery overhead with caching)
- ✅ Native AOT compatible execution engine

### Current Implementation (v0.1-alpha - M1 Complete)
- ✅ **Test execution**: Zero reflection - delegates only
- ✅ **Test discovery**: Minimal reflection - type lookup only, one-time, cached
- ✅ **Source generator**: Emits `GeneratedTestRegistry` with `TestCaseDescriptor[]`
- 🎯 **Future optimization**: Eliminate type discovery reflection (non-critical)

**Architecture Flow**:
```
Compile Time:
  NextUnitGenerator analyzes [Test] attributes
    ↓
  Generates GeneratedTestRegistry.g.cs with delegates
    ↓
  Compiles into test assembly

Runtime (Discovery - One-time):
  Framework finds GeneratedTestRegistry type (cached)
    ↓
  Reads static TestCases property
    ↓
  Builds dependency graph

Runtime (Execution - Zero Reflection):
  Invokes TestMethodDelegate for each test
    ↓
  Pure delegate invocation (no MethodInfo.Invoke)
    ↓
  High performance ✅
```

### Components
- **NextUnit.Core** - Attributes, assertions, test execution engine
- **NextUnit.Generator** - Source generator for test discovery (Complete - M1)
- **NextUnit.Platform** - Microsoft.Testing.Platform integration
- **NextUnit.SampleTests** - Example tests and validation

## Performance Targets (v1.0)

| Metric | Target | Status |
|--------|--------|--------|
| Test discovery (1,000 tests) | <50ms | ✅ Achieved (~2ms with caching) |
| Test execution startup | <100ms | ✅ Achieved (~20ms) |
| Parallel scaling | Linear to core count | ✅ Achieved |
| Framework baseline memory | <10MB | ✅ Achieved (~5MB) |
| Per-test overhead | <1ms | ✅ Achieved (~0.7ms) |
| Assertion overhead | <1μs | 📋 M5 - Planned |

## Documentation

- [PLANS.md](PLANS.md) - Complete implementation roadmap and milestones
- [DEVLOG.md](DEVLOG.md) - Development log and session notes
- [CODING_STANDARDS.md](CODING_STANDARDS.md) - Coding conventions and style guide
- [Attributes Guide](docs/Attributes.md) - Coming soon
- [Assertions Guide](docs/Assertions.md) - Coming soon
- [Lifecycle Guide](docs/Lifecycle.md) - Coming soon

## Contributing

NextUnit is in early development. Contributions welcome!

1. Read [CODING_STANDARDS.md](CODING_STANDARDS.md) - **All code and comments must be in English**
2. Check [PLANS.md](PLANS.md) for current milestones
3. Open an issue to discuss your idea
4. Submit a PR with tests

**Important**: This project follows an **English-only policy** for all code, comments, documentation, and commit messages to ensure international collaboration and consistency with .NET ecosystem standards.

### Development Workflow

**Build Configurations:**
- **Debug**: Lenient settings for fast iteration (warnings allowed)
- **Release**: Strict settings matching CI/CD (warnings as errors)

**Before submitting a PR:**
```bash
# Build in Release mode to catch issues before CI
dotnet build --configuration Release

# Format code to match style guidelines
dotnet format

# Run tests
dotnet run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj
```

**Why two configurations?**
- Debug builds let you iterate quickly without fixing every warning immediately
- Release builds enforce the same strict quality checks as GitHub Actions
- This prevents surprises when your PR fails CI checks

**Tip**: Set Visual Studio to build Release configuration before commits to catch issues early!

## License

[MIT License](LICENSE) - See LICENSE file for details

## Acknowledgments

NextUnit is inspired by:
- **TUnit** - Modern architecture, Microsoft.Testing.Platform integration, source generators
- **xUnit** - Ergonomic assertions, familiar naming, proven patterns
- **NUnit/MSTest** - Battle-tested reliability, clear error messages

## Status & Roadmap

**Current Version**: 0.1-alpha (Development)

**Next Milestones**:
- ✅ M0 - Basic framework (Complete)
- ✅ M1 - Source Generator & Discovery (Complete - 2025-12-02)
- 📋 M1.5 - Parameterized Tests & Skip Support (Next - 2 weeks)
- 📋 M2 - Lifecycle & Execution (4 weeks)
- 📋 M3 - Parallel Scheduler (2 weeks)
- 📋 M4 - Platform Integration (4 weeks)
- 📋 M5 - Assertions & DX (2 weeks)
- 📋 M6 - Documentation & Samples (2 weeks)

**Target v1.0 Preview**: ~20 weeks from now (Early May 2025)

**Latest Progress** (2025-12-02 - M1 Complete):
- ✅ Source generator emits complete test registry with delegates
- ✅ Zero reflection in test execution path (delegates only)
- ✅ Minimal reflection for test discovery (type lookup only, cached)
- ✅ Generator diagnostics (cycle detection, unresolved dependencies)
- ✅ All 20 sample tests passing with generated code
- ✅ Removed ReflectionTestDescriptorBuilder and TestDescriptorProvider
- ✅ M1 milestone complete - ready for M1.5

**M1 Key Achievement**: Zero-reflection test execution with source generator-based test registration. Tests are invoked via delegates, not `MethodInfo.Invoke()`, enabling high performance and Native AOT compatibility.

See [PLANS.md](PLANS.md) for detailed timeline and technical specifications.

---

**Built with ❤️ for .NET 10+ developers who want TUnit's power with xUnit's simplicity**
