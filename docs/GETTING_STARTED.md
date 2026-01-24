# Getting Started with NextUnit

Welcome to NextUnit! This guide will help you get up and running with NextUnit in minutes.

## What is NextUnit?

NextUnit is a modern, high-performance test framework for .NET that combines:

- **Zero-reflection test execution** via source generators
- **xUnit-style assertions** (familiar `Assert.Equal`, `Assert.True`, etc.)
- **Fine-grained parallel execution** with `[ParallelLimit]` and `[NotInParallel]`
- **Multi-scope lifecycle** (Test, Class, Assembly scopes)
- **Native AOT compatibility** for maximum performance

## Installation

### Prerequisites

- .NET 10 or later
- Visual Studio 2026 or VS Code with C# Dev Kit

### Create a New Test Project

```bash
# Create a new class library
dotnet new classlib -n MyProject.Tests -f net10.0

# Navigate to the project directory
cd MyProject.Tests
```

### Add NextUnit Packages

```bash
# Add NextUnit (includes Core, Generator, TestAdapter, and Microsoft.NET.Test.Sdk)
dotnet add package NextUnit
```

### Configure Your Project

Update your `.csproj` file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NextUnit" Version="1.14.0" />
  </ItemGroup>
</Project>
```

**Note**: The `NextUnit` meta-package automatically includes all required dependencies
(NextUnit.Core, NextUnit.Generator, NextUnit.TestAdapter, and Microsoft.NET.Test.Sdk).
No `OutputType=Exe` or `Program.cs` is needed.

## Writing Your First Test

Create a new file `CalculatorTests.cs`:

```csharp
namespace MyProject.Tests;

public class CalculatorTests
{
    [Test]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = 2 + 2;
        Assert.Equal(4, result);
    }

    [Test]
    public void Divide_ByZero_ThrowsException()
    {
        Assert.Throws<DivideByZeroException>(() => 10 / 0);
    }

    [Test]
    [Arguments(1, 2, 3)]
    [Arguments(5, 5, 10)]
    [Arguments(10, -5, 5)]
    public void Add_ParameterizedTests(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }
}
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with no build
dotnet test --no-build

# Run specific tests (using VSTest filter)
dotnet test --filter "FullyQualifiedName~Calculator"
```

### Visual Studio

1. Build your project (Ctrl+Shift+B)
2. Open Test Explorer (Ctrl+E, T)
3. Click "Run All" or right-click specific tests

### VS Code

1. Install "C# Dev Kit" extension
2. Open Test Explorer (Testing icon in sidebar)
3. Click "Run All Tests" or run individual tests

## Common Assertions

NextUnit provides xUnit-compatible assertion methods:

### Basic Assertions

```csharp
Assert.True(condition);               // Verify condition is true
Assert.False(condition);              // Verify condition is false
Assert.Equal(expected, actual);       // Verify equality
Assert.NotEqual(notExpected, actual); // Verify inequality
Assert.Null(value);                   // Verify null
Assert.NotNull(value);                // Verify not null
```

### Collection Assertions

```csharp
Assert.Contains(item, collection);          // Item exists in collection
Assert.DoesNotContain(item, collection);    // Item not in collection
Assert.Empty(collection);                   // Collection is empty
Assert.NotEmpty(collection);                // Collection has elements
Assert.Single(collection);                  // Exactly one element
Assert.All(collection, item => { ... });    // All items satisfy condition
```

### String Assertions

```csharp
Assert.StartsWith("Hello", text);      // Text starts with prefix
Assert.EndsWith("World", text);        // Text ends with suffix
Assert.Contains("substring", text);    // Text contains substring
```

### Numeric Assertions

```csharp
Assert.InRange(value, min, max);       // Value in range [min, max]
Assert.NotInRange(value, min, max);    // Value outside range
```

### Exception Assertions

```csharp
Assert.Throws<Exception>(() => { ... });              // Sync code throws
Assert.ThrowsAsync<Exception>(async () => { ... });   // Async code throws

// New in v1.6: Exception message matching
Assert.Throws<Exception>(() => { ... }, "Expected message");
Assert.ThrowsAsync<Exception>(async () => { ... }, "Expected message");
```

### Approximate Equality Assertions (New in v1.6)

For floating-point comparisons with tolerance:

```csharp
// Compare doubles with precision (decimal places)
Assert.Equal(3.14159, 3.14158, precision: 4);         // Pass
Assert.NotEqual(3.14, 2.71, precision: 2);            // Pass

// Compare decimals with precision
Assert.Equal(100.123456m, 100.123455m, precision: 5); // Pass
Assert.NotEqual(100.0m, 200.0m, precision: 0);        // Pass

// Handles special values
Assert.Equal(double.NaN, double.NaN, precision: 2);
Assert.Equal(double.PositiveInfinity, double.PositiveInfinity, precision: 2);
```

### Collection Comparison Assertions (New in v1.6)

Advanced collection comparisons:

```csharp
// Unordered equality - same elements in any order
var expected = new[] { 1, 2, 3, 4, 5 };
var actual = new[] { 5, 3, 1, 4, 2 };
Assert.Equivalent(expected, actual);

// Subset relationship - all elements of subset in superset
var subset = new[] { 2, 4 };
var superset = new[] { 1, 2, 3, 4, 5 };
Assert.Subset(subset, superset);

// Disjoint collections - no common elements
var collection1 = new[] { 1, 2, 3 };
var collection2 = new[] { 4, 5, 6 };
Assert.Disjoint(collection1, collection2);
```

### Custom Comparers (New in v1.6)

Use custom equality comparers for complex types:

```csharp
// Case-insensitive string comparison
Assert.Equal("hello", "HELLO", StringComparer.OrdinalIgnoreCase);

// Custom comparer for complex types
Assert.Equal(expected, actual, new MyCustomComparer());
```

## Lifecycle Methods

NextUnit supports multi-scope lifecycle methods:

```csharp
public class DatabaseTests
{
    // Runs before each test
    [Before(LifecycleScope.Test)]
    public void SetupTest()
    {
        // Initialize per-test resources
    }

    // Runs after each test
    [After(LifecycleScope.Test)]
    public void CleanupTest()
    {
        // Clean up per-test resources
    }

    // Runs once before all tests in this class
    [Before(LifecycleScope.Class)]
    public void SetupClass()
    {
        // Initialize shared resources
    }

    // Runs once after all tests in this class
    [After(LifecycleScope.Class)]
    public void CleanupClass()
    {
        // Clean up shared resources
    }

    [Test]
    public void FirstTest() { }

    [Test]
    public void SecondTest() { }
}
```

## Parallel Execution

NextUnit runs tests in parallel by default for maximum performance:

```csharp
// This class's tests run serially (not in parallel with each other)
[NotInParallel]
public class SerialTests
{
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }
}

// Limit parallel execution to 4 concurrent tests
[ParallelLimit(4)]
public class LimitedParallelTests
{
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }
}
```

## Test Dependencies

Ensure tests run in a specific order:

```csharp
public class IntegrationTests
{
    [Test]
    public void Step1_Initialize() { }

    [Test]
    [DependsOn(nameof(Step1_Initialize))]
    public void Step2_Process() { }

    [Test]
    [DependsOn(nameof(Step2_Process))]
    public void Step3_Verify() { }
}
```

## Skipping Tests

```csharp
// Skip with reason
[Test]
[Skip("Waiting for bug fix #123")]
public void PendingTest() { }
```

**Note**: Use the `[Skip]` attribute to skip tests at compile time. Runtime conditional skipping is not currently supported.

## Best Practices

1. **Use descriptive test names**: `MethodName_Scenario_ExpectedResult`
2. **Keep tests focused**: One assertion per test when possible
3. **Use parameterized tests**: Reduce duplication with `[Arguments]`
4. **Leverage parallel execution**: Most tests should run in parallel
5. **Use lifecycle scopes wisely**: Share expensive setup at class/assembly level
6. **Add skip reasons**: Always explain why a test is skipped

## Next Steps

- Read [Best Practices Guide](BEST_PRACTICES.md) for advanced patterns
- See [Migration Guide](MIGRATION_FROM_XUNIT.md) if coming from xUnit
- Check [Performance Analysis](PERFORMANCE.md) for benchmarks and optimization tips
- View [CI/CD Integration](CI_CD_INTEGRATION.md) for continuous integration setup

## Getting Help

- **GitHub Issues**: <https://github.com/crane-valley/NextUnit/issues>
- **Documentation**: <https://github.com/crane-valley/NextUnit/wiki>
- **Examples**: See `samples/NextUnit.SampleTests` in the repository

## What's Different from xUnit?

| Feature | xUnit | NextUnit |
| ------- | ----- | -------- |
| Test Attribute | `[Fact]` | `[Test]` |
| Parameterized | `[Theory]` + `[InlineData]` | `[Test]` + `[Arguments]` |
| Discovery | Reflection at runtime | Source generator (faster) |
| Parallelism | Configurable, limited | Fine-grained with `[ParallelLimit]` |
| Lifecycle | Constructor + `IDisposable` | Multi-scope `[Before]`/`[After]` |
| AOT Support | Limited | Full Native AOT compatible |

Welcome to the future of .NET testing!
