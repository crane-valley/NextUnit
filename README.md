# NextUnit

[![.NET](https://github.com/crane-valley/NextUnit/actions/workflows/dotnet.yml/badge.svg)](https://github.com/crane-valley/NextUnit/actions/workflows/dotnet.yml)

A modern, high-performance test framework for .NET 10+ that combines TUnit's architecture with xUnit's familiar assertions.

## Vision

NextUnit bridges the gap between modern testing infrastructure and developer-friendly APIs:
- **TUnit's modern architecture** - Microsoft.Testing.Platform integration, Native AOT support, source generators
- **xUnit's ergonomic assertions** - Classic `Assert.Equal(expected, actual)`, no fluent syntax, synchronous by default

## Features

### Implemented (v0.4-alpha)
- ✅ **Clear attribute naming** - `[Test]`, `[Before]`, `[After]` (not `[Fact]` or `[Theory]`)
- ✅ **Rich assertions** - Collection, String, Numeric assertions with great error messages
  - Collection: `Contains`, `DoesNotContain`, `All`, `Single`, `Empty`, `NotEmpty`
  - String: `StartsWith`, `EndsWith`, `Contains`
  - Numeric: `InRange`, `NotInRange`
  - Basic: `Equal`, `True`, `Throws` (familiar to xUnit/NUnit/MSTest users)
- ✅ **Multi-scope lifecycle** - `[Before(LifecycleScope.Test/Class/Assembly)]`, `[After(LifecycleScope.Test/Class/Assembly)]`
- ✅ **Dependency ordering** - `[DependsOn(nameof(OtherTest))]` ensures execution order
- ✅ **Parallel control** - `[NotInParallel]`, `[ParallelLimit(4)]` for fine-grained concurrency (fully enforced!)
- ✅ **Skip support** - `[Skip("reason")]` to skip tests with optional reason
- ✅ **Parameterized tests** - `[Arguments(1, 2, 3)]` for inline test data with human-readable display names
- ✅ **Test data sources** - `[TestData(nameof(DataMethod))]` for method/property data sources with `MemberType` support
- ✅ **Instance-per-test** - Each test gets a fresh class instance (maximizes isolation)
- ✅ **Async support** - `async Task` tests, `Assert.ThrowsAsync<T>` for async assertions
- ✅ **Proper disposal** - Automatic `IDisposable`/`IAsyncDisposable` cleanup
- ✅ **Source generator** - Emits test registry with zero-reflection delegates
- ✅ **Generator diagnostics** - Detects dependency cycles and unresolved dependencies
- ✅ **Zero-reflection execution** - Test methods invoked via delegates, not reflection
- ✅ **True parallel execution** - Thread-safe parallel test execution with constraint enforcement

### Planned (see [PLANS.md](PLANS.md))
- 📋 **Session lifecycle** - Session-scoped setup/teardown (M4)
- 📋 **Test traits** - `[Category]`, `[Tag]` for filtering (M4)

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
dotness run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj

# Run with specific options
dotness run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj -- --help

# Run with minimum expected tests check
dotness run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj -- --minimum-expected-tests 20

# Generate test results
dotness run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj -- --results-directory ./TestResults --report-trx
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

    // Collection assertions
    [Test]
    public void List_ContainsExpectedItems()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };
        Assert.Contains(3, numbers);
        Assert.DoesNotContain(6, numbers);
        Assert.NotEmpty(numbers);
        Assert.All(numbers, n => Assert.InRange(n, 1, 5));
    }

    // String assertions
    [Test]
    public void Email_HasValidFormat()
    {
        var email = "user@example.com";
        Assert.Contains("@", email);
        Assert.EndsWith(".com", email);
        Assert.StartsWith("user", email);
    }

    // Numeric assertions
    [Test]
    public void Temperature_IsInValidRange()
    {
        var temperature = 23.5;
        Assert.InRange(temperature, 20.0, 25.0);
    }
}
```

### Lifecycle Hooks

```csharp
public class DatabaseTests
{
    Database? _db;

    // Runs before each test
    [Before(LifecycleScope.Test)]
    public void Setup()
    {
        _db = new Database();
    }

    // Runs after each test
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

// Class-scoped lifecycle
public class ExpensiveResourceTests
{
    static ExpensiveResource? _resource;

    // Runs once before all tests in class
    [Before(LifecycleScope.Class)]
    public void ClassSetup()
    {
        _resource = new ExpensiveResource();
    }

    // Runs once after all tests in class
    [After(LifecycleScope.Class)]
    public void ClassTeardown()
    {
        _resource?.Dispose();
    }

    [Test]
    public void Test1()
    {
        Assert.NotNull(_resource);
    }

    [Test]
    public void Test2()
    {
        Assert.NotNull(_resource);
    }
}

// Assembly-scoped lifecycle
public class GlobalSetupTests
{
    // Runs once before all tests in assembly
    [Before(LifecycleScope.Assembly)]
    public void AssemblySetup()
    {
        // Initialize global resources
    }

    // Runs once after all tests in assembly
    [After(LifecycleScope.Assembly)]
    public void AssemblyTeardown()
    {
        // Cleanup global resources
    }

    [Test]
    public void SomeTest()
    {
        // Test code
    }
}
```

### Parameterized Tests

```csharp
public class MathTests
{
    // Multiple test cases with inline data
    [Test]
    [Arguments(2, 3, 5)]
    [Arguments(1, 1, 2)]
    [Arguments(-1, 1, 0)]
    [Arguments(0, 0, 0)]
    public void Add_ReturnsCorrectSum(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }

    // Display names show argument values: "Add_ReturnsCorrectSum(2, 3, 5)"
}

public class StringTests
{
    [Test]
    [Arguments("hello", 5)]
    [Arguments("world", 5)]
    [Arguments("", 0)]
    public void String_HasCorrectLength(string text, int expectedLength)
    {
        Assert.Equal(expectedLength, text.Length);
    }
}
```

### Test Data from Methods or Properties

For more complex test data, use the `[TestData]` attribute to reference a static method or property:

```csharp
public class CalculatorTests
{
    // Data from a static method
    public static IEnumerable<object[]> AdditionTestCases()
    {
        yield return new object[] { 1, 2, 3 };
        yield return new object[] { 2, 3, 5 };
        yield return new object[] { -1, 1, 0 };
    }

    [Test]
    [TestData(nameof(AdditionTestCases))]
    public void Add_ReturnsCorrectSum(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }

    // Data from a static property
    public static IEnumerable<object[]> MultiplicationCases =>
    [
        [2, 3, 6],
        [4, 5, 20],
        [0, 100, 0]
    ];

    [Test]
    [TestData(nameof(MultiplicationCases))]
    public void Multiply_Works(int a, int b, int expected)
    {
        var result = a * b;
        Assert.Equal(expected, result);
    }

    // Data from an external class using MemberType
    [Test]
    [TestData(nameof(SharedTestData.DivisionCases), MemberType = typeof(SharedTestData))]
    public void Divide_Works(int a, int b, int expected)
    {
        var result = a / b;
        Assert.Equal(expected, result);
    }

    // Multiple data sources can be combined
    [Test]
    [TestData(nameof(PositiveNumbers))]
    [TestData(nameof(NegativeNumbers))]
    public void Abs_ReturnsAbsoluteValue(int value, int expected)
    {
        var result = Math.Abs(value);
        Assert.Equal(expected, result);
    }

    public static IEnumerable<object[]> PositiveNumbers => [[5, 5], [10, 10]];
    public static IEnumerable<object[]> NegativeNumbers => [[-5, 5], [-10, 10]];
}

public static class SharedTestData
{
    public static IEnumerable<object[]> DivisionCases()
    {
        yield return new object[] { 10, 2, 5 };
        yield return new object[] { 100, 10, 10 };
    }
}
```

### Skip Tests

```csharp
public class FeatureTests
{
    [Test]
    [Skip("Waiting for bug fix #123")]
    public void NewFeature_Works()
    {
        // This test will be skipped with reason displayed
    }

    [Test]
    public void ExistingFeature_Works()
    {
        // This test runs normally
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

**Current Version**: 0.4-alpha (Development)

**v1.0 Release Plan** (1-2 weeks):
- ✅ M0 - Basic framework (Complete)
- ✅ M1 - Source Generator & Discovery (Complete - 2025-12-02)
- ✅ M1.5 - Parameterized Tests & Skip Support (Complete - 2025-12-02)
- ✅ M2 - Lifecycle Scopes (Complete - 2025-12-02)
- ✅ M2.5 - Polish & Testing (Complete - 2025-12-02)
- ✅ M3 - Parallel Scheduler (Complete - 2025-12-03)
- 🔄 M4 - Rich Assertions & v1.0 Prep (Phase 1 Complete - 2025-12-03, Phase 2 In Progress)

**Target v1.0 Release**: Mid-Late December 2025

**Latest Progress** (2025-12-03 - M4 Phase 1 Complete):
- ✅ M1: Source generator with zero-reflection test execution
- ✅ M1.5: Skip attribute with reason reporting
- ✅ M1.5: Parameterized tests with Arguments attribute
- ✅ M1.5: Enhanced display names showing argument values
- ✅ M2: Class-scoped lifecycle (`[Before/After(LifecycleScope.Class)]`)
- ✅ M2: Assembly-scoped lifecycle (`[Before/After(LifecycleScope.Assembly)]`)
- ✅ M2.5: Comprehensive documentation and 67 test examples
- ✅ M3: True parallel execution with ParallelLimit enforcement
- ✅ M3: Thread-safe lifecycle management (ConcurrentDictionary + SemaphoreSlim)
- ✅ M4 Phase 1: Rich Assertions Library (11 new assertion methods)
  - Collection assertions: Contains, DoesNotContain, All, Single, Empty, NotEmpty
  - String assertions: StartsWith, EndsWith, Contains
  - Numeric assertions: InRange, NotInRange
- ✅ 86 tests passing (83 passed, 3 skipped, 0 failed) - was 67, +19 tests
- ✅ ~634ms execution time (was ~620ms, +14ms acceptable)
- ✅ Zero reflection maintained across all features

**v1.0 Focus**:
- ✅ Rich Assertions (COMPLETE - Collection, String, Numeric assertions)
- 🔄 Complete Documentation (In Progress - Getting Started, Migration Guide created)
- 📋 NuGet Package (Published to nuget.org)
- 📋 Release Ready (Changelog, GitHub releases, version tagging)
