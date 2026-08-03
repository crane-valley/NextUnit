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
# Install the template package once per machine
dotnet new install NextUnit.Templates

# Create a test project with one passing example test
dotnet new nextunit -n MyProject.Tests
cd MyProject.Tests
```

`dotnet new nextunit` produces the project described below, so you can skip straight to
[Writing Your First Test](#writing-your-first-test). The remaining steps in this section cover the
manual setup for an existing project.

```bash
# Create a new class library
dotnet new classlib -n MyProject.Tests -f net10.0

# Navigate to the project directory
cd MyProject.Tests
```

### Add NextUnit Packages

```bash
# Add the complete NextUnit package
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
    <PackageReference Include="NextUnit" Version="1.18.0" />
  </ItemGroup>
</Project>
```

**Note**: The `NextUnit` meta-package automatically includes all required dependencies
(runtime, platform integration, source generator, analyzers, and TRX reporting).
No `OutputType=Exe`, `Program.cs`, or separate analyzer reference is needed.

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

### Typed Data Rows

Use `TestDataRow<T>` when individual rows need their own display name, filtering metadata, or skip
reason. Tuple data is expanded across the test method parameters without changing the method
signature:

```csharp
public static IEnumerable<TestDataRow<(int A, int B, int Expected)>> AdditionRows()
{
    yield return new(
        (2, 3, 5),
        displayName: "adds positive values",
        categories: ["Arithmetic"],
        tags: ["Smoke"]);
    yield return new(
        (1, -1, 0),
        displayName: "adds mixed-sign values",
        skipReason: "Tracked issue");
}

[Test]
[TestData(nameof(AdditionRows))]
public void Add_TypedRows(int a, int b, int expected)
{
    Assert.Equal(expected, a + b);
}
```

`TestDataRow<T>` works with both `[TestData]` members and `[ClassDataSource<T>]` enumerables.
Method- and class-level categories or tags are combined with row metadata. A row display name and
skip reason apply only to that generated test case. Tuple values expand across parameters; other
values, including `null` and collections, remain a single argument.

### Async Data Rows

A `[TestData]` member can produce its rows asynchronously. Three shapes are supported:
`IAsyncEnumerable<T>`, `Task<TCollection>`, and `ValueTask<TCollection>`, where `TCollection` is any
enumerable. Rows may be typed exactly as they are for a synchronous member, `TestDataRow<T>`
included.

An `IAsyncEnumerable<T>` member may take a single `CancellationToken` parameter. NextUnit passes
whichever token governs the enumeration -- the discovery token here, or the run token for a deferred
source -- so a source that waits on I/O can be interrupted instead of stalling the run:

```csharp
public static async IAsyncEnumerable<object[]> StreamedRows(
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await using var reader = OpenFixtureStream();
    await foreach (var line in reader.ReadLinesAsync(cancellationToken))
    {
        yield return Parse(line);
    }
}

[Test]
[TestData(nameof(StreamedRows))]
public void Add_FromAsyncSource(int a, int b, int expected)
{
    Assert.Equal(expected, a + b);
}
```

A member that fetches everything at once returns the collection wrapped in a task instead:

```csharp
public static async Task<IEnumerable<object[]>> LoadRowsAsync()
{
    var payload = await httpClient.GetFromJsonAsync<Row[]>("fixtures.json");
    return payload!.Select(row => new object[] { row.A, row.B, row.Expected });
}
```

Rows are enumerated once during discovery, exactly as synchronous rows are, so every row stays an
individually selectable and filterable test case in the IDE and on the command line. The source
generator binds the member statically, so async sources need no runtime reflection and remain
trimming- and Native AOT-compatible.

The awaited value has to be a collection of rows. A member returning bare `Task`, or a task wrapping
something that is not enumerable such as `Task<int>`, cannot supply rows and is reported at build
time as `NU0014`.

A data source must not block synchronously. Cancellation is honored at every genuine await point, so
a source that awaits its I/O can always be interrupted. Code that blocks the calling thread cannot
be: `Task.Wait()`, `.Result`, a lock held across a slow call, or a lazy sequence whose `MoveNext`
blocks will stall whichever phase is enumerating the source -- discovery, or the run itself for a
deferred source -- until it returns, and no cancellation token can shorten that. This is not
specific to async sources -- an ordinary `IEnumerable<T>` member that blocks behaves the same way --
but it is worth stating plainly, because an `async` signature can otherwise suggest a guarantee the
runtime cannot make. Await instead of blocking, and observe the token you are given.

### Deferred Data Rows

Eager enumeration is the default and stays the default. Every `[TestData]` member is read once during
discovery, which is what makes each row an individually selectable, filterable test case. For a
source large or slow enough that reading it at startup is itself the problem, set
`DeferredEnumeration = true` to move the enumeration to execution time:

```csharp
public static IEnumerable<object[]> EveryRowInTheFixtureFile()
{
    foreach (var line in File.ReadLines("ten-thousand-rows.csv"))
    {
        yield return Parse(line);
    }
}

[Test]
[TestData(nameof(EveryRowInTheFixtureFile), DeferredEnumeration = true)]
public void Validate(int input, int expected)
{
    Assert.Equal(expected, Transform(input));
}
```

Discovery then reports one placeholder test named after the source -- `Validate (deferred data
source: EveryRowInTheFixtureFile)` -- instead of one test per row, and the member is not called at
all. The rows become individual test results when the run reaches the placeholder. Deferral is
independent of row shape: a deferred `IAsyncEnumerable<T>` member enumerates during execution and
receives the run cancellation token rather than the discovery one.

#### The selection and filtering tradeoff

Nothing but the placeholder exists until the run starts, so a deferred source is selected, filtered,
and skipped as one unit:

- Row-level display names, categories, and tags cannot be filtered on, because they do not exist yet
  when the filter is evaluated. Filters still apply to the group through the test method's own name,
  categories, and tags, so `--test-name "Validate*"` runs the whole source.
- An IDE cannot run a single row of a deferred source. Selecting the placeholder runs every row.
- `--list-tests` reports one entry for the source rather than one per row, and cannot report a row
  count.
- A skipped test does not enumerate its source at all; the skip is reported once, on the placeholder.

A filter is deliberately never allowed to re-enable eager enumeration behind your back. Doing so
would restore the exact startup cost the option exists to remove, and would restore it precisely when
you were trying to narrow the run. If you want per-row selection, use eager enumeration -- that is
what it is for.

What does not change is the rows themselves. Once expanded they are ordinary test cases, so retry,
timeout, parallelism, dependencies, priority, and typed `TestDataRow<T>` metadata behave exactly as
they do for an eager source. A source that throws is reported as an error on the placeholder, and the
rest of the run continues rather than the whole assembly failing.

What does change, besides the timing, is where the read sits in the lifecycle. An eager source is
read while the test list is built, before any hook runs. A deferred source is read at the start of the
run: under Microsoft.Testing.Platform that is after session-scoped setup and before assembly-, class-,
and test-scoped hooks, and under the VSTest adapter, which has no session scope, nothing has run
either. A deferred source is also not read at all when a session setup hook requested a skip, or when
the test itself is skipped. Do not write a data source that depends on lifecycle state; the ordering
differs between the two modes and is not a contract to build on.

## Running Tests

### Command Line

```bash
# Run this test project without repository-level configuration
dotnet run

# Run with no build
dotnet run --no-build

# Run specific tests
dotnet run -- --test-name "*Calculator*"
```

To use `dotnet test` across a repository with the .NET 10 SDK, add this `global.json` at the
repository root:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Then run `dotnet test`, or `dotnet test --project MyProject.Tests.csproj` for one project.

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
Assert.NotInRange(value, min, max);    // Value outside range [min, max]; min and max are inside
```

### Exception Assertions

```csharp
Assert.Throws<Exception>(() => { ... });              // Sync code throws
Assert.ThrowsAsync<Exception>(async () => { ... });   // Async code throws

// The second string argument is a custom failure message, not an expected exception message.
Assert.Throws<Exception>(() => { ... }, "Custom failure message");

// To check the exception message, assert on the returned exception.
var ex = Assert.Throws<ArgumentException>(() => { ... });
Assert.Equal("Expected message", ex.Message);
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

## Execution Priority

Control test execution order within the same dependency level:

```csharp
public class PriorityTests
{
    // Higher priority values run first
    [Test]
    [ExecutionPriority(100)]
    public void HighPriorityTest() { }

    [Test]
    [ExecutionPriority(10)]
    public void LowPriorityTest() { }

    [Test]  // Default priority is 0
    public void DefaultPriorityTest() { }
}

// Class-level priority sets default for all tests in the class
[ExecutionPriority(50)]
public class ModeratelyImportantTests
{
    [Test]
    public void Test1() { }  // Priority 50

    [Test]
    [ExecutionPriority(100)]  // Override class level
    public void ImportantTest() { }  // Priority 100
}
```

**Note**: Execution priority only affects tests at the same dependency level. Tests with
`[DependsOn]` still wait for their dependencies regardless of priority.

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
