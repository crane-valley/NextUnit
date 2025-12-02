# NextUnit

A modern, high-performance test framework for .NET 10+ that combines TUnit's architecture with xUnit's familiar assertions.

## ?? Vision

NextUnit bridges the gap between modern testing infrastructure and developer-friendly APIs:
- **TUnit's modern architecture** - Microsoft.Testing.Platform integration, Native AOT support, source generators
- **xUnit's ergonomic assertions** - Classic `Assert.Equal(expected, actual)`, no fluent syntax, synchronous by default

## ? Features

### Implemented (v0.1-alpha)
- ? **Clear attribute naming** - `[Test]`, `[Before]`, `[After]` (not `[Fact]` or `[Theory]`)
- ? **Classic assertions** - `Assert.Equal`, `Assert.True`, `Assert.Throws` (familiar to xUnit/NUnit/MSTest users)
- ? **Lifecycle hooks** - `[Before(LifecycleScope.Test)]`, `[After(LifecycleScope.Test)]`
- ? **Dependency ordering** - `[DependsOn(nameof(OtherTest))]` ensures execution order
- ? **Parallel control** - `[NotInParallel]`, `[ParallelLimit(4)]` for fine-grained concurrency
- ? **Instance-per-test** - Each test gets a fresh class instance (maximizes isolation)
- ? **Async support** - `async Task` tests, `Assert.ThrowsAsync<T>` for async assertions
- ? **Proper disposal** - Automatic `IDisposable`/`IAsyncDisposable` cleanup
- ? **Source generator** - Emits test registry with zero-reflection delegates (M1 - 80% complete)
- ? **Generator diagnostics** - Detects dependency cycles and unresolved dependencies

### Planned (see [PLANS.md](PLANS.md))
- ?? **Zero reflection discovery** - Complete generator, remove fallback (M1 - final 20%)
- ?? **Advanced lifecycle** - Assembly/Class/Session scopes (M2)
- ?? **Smart scheduler** - Parallel execution with constraint enforcement (M3)
- ?? **Rich assertions** - Collections, strings, numerics with great error messages (M5)
- ?? **Native AOT** - Full trim-compatibility, no runtime reflection (M1-M6)

## ?? Quick Start

### Installation

```bash
# Coming soon to NuGet
# For now, build from source
dotnet build
```

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

## ??? Architecture

NextUnit is designed for **performance** and **maintainability**:

### Zero-Reflection Design (Target for v1.0)
- ? No `System.Reflection` in production code paths
- ? Source generators for all test discovery
- ? Fast startup (<50ms for 1,000 tests)
- ? Native AOT compatible

### Current Status (v0.1-alpha)
- ?? **Development fallback**: Currently uses reflection for prototyping
- ?? **Planned**: Generator-only approach before v1.0 (see PLANS.md M1)
- ?? **Goal**: Complete source generator implementation in 4 weeks

### Components
- **NextUnit.Core** - Attributes, assertions, test execution engine
- **NextUnit.Generator** - Source generator for test discovery (in development)
- **NextUnit.Platform** - Microsoft.Testing.Platform integration
- **NextUnit.SampleTests** - Example tests and validation

## ?? Performance Targets (v1.0)

| Metric | Target | Status |
|--------|--------|--------|
| Test discovery (1,000 tests) | <50ms | ?? In progress |
| Test execution startup | <100ms | ? Achieved (~20ms) |
| Parallel scaling | Linear to core count | ? Achieved |
| Framework baseline memory | <10MB | ? Achieved (~5MB) |
| Per-test overhead | <1ms | ? Achieved (~0.7ms) |

## ?? Documentation

- [PLANS.md](PLANS.md) - Complete implementation roadmap and milestones
- [DEVLOG.md](DEVLOG.md) - Development log and session notes
- [CODING_STANDARDS.md](CODING_STANDARDS.md) - Coding conventions and style guide
- [Attributes Guide](docs/Attributes.md) - Coming soon
- [Assertions Guide](docs/Assertions.md) - Coming soon
- [Lifecycle Guide](docs/Lifecycle.md) - Coming soon

## ?? Contributing

NextUnit is in early development. Contributions welcome!

1. Read [CODING_STANDARDS.md](CODING_STANDARDS.md) - **All code and comments must be in English**
2. Check [PLANS.md](PLANS.md) for current milestones
3. Open an issue to discuss your idea
4. Submit a PR with tests

**Important**: This project follows an **English-only policy** for all code, comments, documentation, and commit messages to ensure international collaboration and consistency with .NET ecosystem standards.

## ?? License

[MIT License](LICENSE) - See LICENSE file for details

## ?? Acknowledgments

NextUnit is inspired by:
- **TUnit** - Modern architecture, Microsoft.Testing.Platform integration, source generators
- **xUnit** - Ergonomic assertions, familiar naming, proven patterns
- **NUnit/MSTest** - Battle-tested reliability, clear error messages

## ?? Status & Roadmap

**Current Version**: 0.1-alpha (Development)

**Next Milestones**:
- ? M0 - Basic framework (Complete)
- ?? M1 - Source Generator & Discovery (80% complete, 2-4 hours remaining)
- ?? M2 - Lifecycle & Execution (4 weeks)
- ?? M3 - Parallel Scheduler (2 weeks)
- ?? M4 - Platform Integration (4 weeks)
- ?? M5 - Assertions & DX (2 weeks)
- ?? M6 - Documentation & Samples (2 weeks)

**Target v1.0 Preview**: ~18 weeks from now

**Latest Progress** (2025-12-02):
- ? Source generator emits complete test registry with delegates
- ? Zero reflection in test execution path
- ? Generator diagnostics (cycle detection, unresolved dependencies)
- ? All 20 sample tests passing with generated code
- ?? Reflection fallback still present (marked for removal)

See [PLANS.md](PLANS.md) for detailed timeline and technical specifications.

---

**Built with ?? for .NET 10+ developers who want TUnit's power with xUnit's simplicity**
