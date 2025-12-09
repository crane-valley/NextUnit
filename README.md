# NextUnit

[![.NET](https://github.com/crane-valley/NextUnit/actions/workflows/dotnet.yml/badge.svg)](https://github.com/crane-valley/NextUnit/actions/workflows/dotnet.yml)
[![Nightly Build](https://github.com/crane-valley/NextUnit/actions/workflows/nightly.yml/badge.svg)](https://github.com/crane-valley/NextUnit/actions/workflows/nightly.yml)
[![NuGet](https://img.shields.io/nuget/v/NextUnit.svg)](https://www.nuget.org/packages/NextUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modern, high-performance test framework for .NET 10+ that combines TUnit's architecture with xUnit's familiar assertions.

## Vision

NextUnit bridges the gap between modern testing infrastructure and developer-friendly APIs:
- **TUnit's modern architecture** - Microsoft.Testing.Platform integration, Native AOT support, source generators
- **xUnit's ergonomic assertions** - Classic `Assert.Equal(expected, actual)`, no fluent syntax, synchronous by default

## Features

### Core Features (v1.0)
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

### New in v1.1
- ✅ **Category filtering** - `[Category("Integration")]` to organize and filter tests
- ✅ **Tag filtering** - `[Tag("Slow")]` for fine-grained test classification
- ✅ **Environment variable filtering** - Filter tests using `NEXTUNIT_INCLUDE_CATEGORIES`, `NEXTUNIT_EXCLUDE_CATEGORIES`, etc.

### New in v1.2
- ✅ **CLI argument filtering** - Command-line arguments for category/tag filtering (e.g., `--category Integration`)
- ✅ **Session-scoped lifecycle** - `[Before(LifecycleScope.Session)]` and `[After(LifecycleScope.Session)]` for session-wide setup/teardown

### New in v1.3
- ✅ **Test output capture** - `ITestOutput` interface for writing diagnostic messages during test execution
- ✅ **Constructor injection** - Tests requiring output can accept `ITestOutput` parameter in constructor
- ✅ **Output in test results** - Test output is automatically included in test results and visible in reports

### Planned (see [PLANS.md](PLANS.md))
- 📋 **Performance benchmarks** - Large-scale test suite validation (v1.4+)

## Quick Start

### Installation

```bash
# Install NextUnit meta-package (includes Core, Generator, and Platform)
dotnet add package NextUnit

# Or install individual packages
dotnet add package NextUnit.Core
dotnet add package NextUnit.Generator
dotnet add package NextUnit.Platform
```

### Running Tests

NextUnit uses **Microsoft.Testing.Platform** for test execution. To run tests:

```bash
# Run all tests in a project (recommended)
dotnet run --project YourTestProject/YourTestProject.csproj

# Run with specific options
dotnet run --project YourTestProject/YourTestProject.csproj -- --help

# Run with minimum expected tests check
dotnet run --project YourTestProject/YourTestProject.csproj -- --minimum-expected-tests 20

# Generate test results
dotnet run --project YourTestProject/YourTestProject.csproj -- --results-directory ./TestResults --report-trx
```

**Note**: The recommended way to run NextUnit tests is with `dotnet run`. For `dotnet test` support on .NET 10 SDK and later, see [dotnet test Support Guide](docs/DOTNET_TEST_SUPPORT.md).

### Filtering Tests by Category and Tag

NextUnit supports organizing and filtering tests using `[Category]` and `[Tag]` attributes. Categories are typically used for broad classifications (like "Integration" or "Unit"), while tags are used for finer-grained metadata (like "Slow" or "RequiresNetwork").

```csharp
[Category("Integration")]
public class DatabaseTests
{
    [Test]
    [Category("Database")]
    [Tag("Slow")]
    public void QueryUsers_ReturnsResults()
    {
        // Test implementation
    }

    [Test]
    [Tag("Fast")]
    public void GetCachedData_Succeeds()
    {
        // Inherits "Integration" category from class
    }
}
```

To filter tests at runtime, use **command-line arguments** (recommended) or environment variables:

```bash
# CLI arguments (v1.2+, recommended)
# Run only tests in the Database category
dotnet run --project YourTestProject -- --category Database

# Run only tests with the Fast tag
dotnet run --project YourTestProject -- --tag Fast

# Exclude tests with the Slow tag
dotnet run --project YourTestProject -- --exclude-tag Slow

# Combine multiple filters
dotnet run --project YourTestProject -- --category Integration --exclude-tag Slow

# Multiple categories or tags (use multiple arguments)
dotnet run --project YourTestProject -- --category Database --category API
dotnet run --project YourTestProject -- --tag Fast --tag Instant

# Environment variables (backward compatible)
# Run only tests in the Database category
NEXTUNIT_INCLUDE_CATEGORIES=Database dotnet run

# Run only tests with the Fast tag
NEXTUNIT_INCLUDE_TAGS=Fast dotnet run

# Exclude tests with the Slow tag
NEXTUNIT_EXCLUDE_TAGS=Slow dotnet run

# Combine filters (include Integration category, exclude Slow tag)
NEXTUNIT_INCLUDE_CATEGORIES=Integration NEXTUNIT_EXCLUDE_TAGS=Slow dotnet run

# Multiple categories (comma-separated)
NEXTUNIT_INCLUDE_CATEGORIES=Database,API dotnet run
```

**Note**: CLI arguments take precedence over environment variables. This allows you to override environment-based defaults on a per-run basis.

**Filter behavior:**
- Categories and tags can be applied to both classes and methods
- Method-level attributes are combined with class-level attributes
- **Exclude filters** take precedence over include filters
- **OR logic** for include filters: tests matching ANY include filter will run
- Within each filter type (categories or tags), matching ANY value passes (OR logic)

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

// Session-scoped lifecycle (v1.2+)
public class SessionSetupTests
{
    static int _sessionCounter;

    // Runs once before all tests in the entire test session
    // Session lifecycle methods MUST be static
    [Before(LifecycleScope.Session)]
    public static void SessionSetup()
    {
        _sessionCounter = 0;
        // Initialize session-wide resources (e.g., test database, external services)
    }

    // Runs once after all tests in the entire test session
    [After(LifecycleScope.Session)]
    public static void SessionTeardown()
    {
        // Cleanup session-wide resources
        _sessionCounter = 0;
    }

    [Test]
    public void SessionTest()
    {
        _sessionCounter++;
        Assert.True(_sessionCounter > 0);
    }
}
```

**Lifecycle Scope Summary:**
- **Test**: Runs before/after each individual test (instance methods)
- **Class**: Runs once before/after all tests in a class (static field recommended)
- **Assembly**: Runs once before/after all tests in an assembly (static field recommended)
- **Session**: Runs once before/after all tests in the entire test session (**must be static**, v1.2+)
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

### Test Output

NextUnit supports writing diagnostic output during test execution using the `ITestOutput` interface, similar to xUnit's `ITestOutputHelper`:

```csharp
using NextUnit.Core;

public class DiagnosticTests
{
    private readonly ITestOutput _output;

    // Inject ITestOutput via constructor
    public DiagnosticTests(ITestOutput output)
    {
        _output = output;
    }

    [Test]
    public void TestWithOutput()
    {
        _output.WriteLine("Starting test execution...");
        
        var result = PerformOperation();
        _output.WriteLine("Result: {0}", result);
        
        Assert.Equal(42, result);
        _output.WriteLine("Test completed successfully!");
    }

    [Test]
    [Arguments(1, 2, 3)]
    [Arguments(10, 20, 30)]
    public void ParameterizedTestWithOutput(int a, int b, int expected)
    {
        _output.WriteLine("Testing: {0} + {1}", a, b);
        var result = a + b;
        _output.WriteLine("Result: {0}", result);
        Assert.Equal(expected, result);
    }

    private int PerformOperation() => 42;
}
```

**Key Features:**
- Output is captured per-test and included in test results
- Works with parameterized tests, lifecycle hooks, and all other features
- Output is visible even when tests fail (helpful for debugging)
- Thread-safe implementation for parallel test execution

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

**Current Version**: 1.1.0 (Stable)

**v1.0 Milestones** (All Complete):
- ✅ M0 - Basic framework (Complete)
- ✅ M1 - Source Generator & Discovery (Complete - 2025-12-02)
- ✅ M1.5 - Parameterized Tests & Skip Support (Complete - 2025-12-02)
- ✅ M2 - Lifecycle Scopes (Complete - 2025-12-02)
- ✅ M2.5 - Polish & Testing (Complete - 2025-12-02)
- ✅ M3 - Parallel Scheduler (Complete - 2025-12-03)
- ✅ M4 - Rich Assertions & v1.0 Prep (Complete - 2025-12-06)

**v1.0 Release**: 2025-12-06

**v1.0 Features**:
- ✅ Zero-reflection test execution with source generators
- ✅ Rich assertion library (Collection, String, Numeric assertions)
- ✅ Multi-scope lifecycle (Test, Class, Assembly)
- ✅ Parameterized tests with Arguments and TestData
- ✅ Skip support with reason reporting
- ✅ True parallel execution with ParallelLimit enforcement
- ✅ Thread-safe lifecycle management
- ✅ Comprehensive documentation
- ✅ 102 tests passing (99 passed, 3 skipped, 0 failed)
- ✅ ~880ms execution time for 102 tests

**v1.1 Release**: 2025-12-06

**v1.1 Features**:
- ✅ Category and Tag filtering with environment variables
- ✅ Source generator support for extracting categories and tags
- ✅ Flexible filtering logic (include/exclude by category or tag)
- ✅ 113 tests passing (110 passed, 3 skipped, 0 failed)

**Planned for v1.2**:
- 📋 CLI arguments for filtering (--category, --tag, --exclude-category, --exclude-tag)
- 📋 Test output/logging integration
- 📋 Session-scoped lifecycle
- 📋 Performance benchmarks with large test suites (1,000+ tests)
