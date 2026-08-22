# Getting Started with NextUnit

Welcome to NextUnit! This guide will help you get up and running with NextUnit in minutes.

This guide, like the rest of the documentation on `main`, describes NextUnit as it stands there while
pinning the latest released version, so between releases it can mention an API the pinned version
does not ship yet; to read it as it stood for an earlier release, check out that release's git tag.

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
    <PackageReference Include="NextUnit" Version="3.0.0" />
  </ItemGroup>
</Project>
```

**Note**: The `NextUnit` meta-package automatically includes all required dependencies
(runtime, platform integration, source generator, analyzers, and TRX reporting).
No `OutputType=Exe`, `Program.cs`, or separate analyzer reference is needed.

## Writing Your First Test

Create a new file `CalculatorTests.cs`:

```csharp
using NextUnit;

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

A source type may offer more than one row type, for instance by implementing both
`IEnumerable<object[]>` and `IEnumerable<TestDataRow<T>>`. Build-time row validation reads it through
the `TestDataRow<T>` arm, because that is the more specific contract: it carries each row's metadata
as well as its values. If a source offers several row types and none of them is a `TestDataRow<T>`,
the one whose fully qualified type name sorts first ordinally is used. The rule depends only on the
types themselves, so the row type a source is checked against does not change between builds. The
selection governs what `NU0009` validates; a synchronous source is still enumerated through the
non-generic `IEnumerable` at run time, so prefer a source type that offers one row type.

### Data source member accessibility

A `[TestData]` or `[ValuesFromMember]` member must be reachable from the generated test registry,
which is compiled into your test assembly. `public` and `internal` members qualify, as do members of
`internal` types. A `private`, `protected`, or `private protected` member does not, nor does a
property whose getter is not accessible or which has no getter, nor a member of a type nested in a
private or protected scope; those are reported at build time as `NU0020`. `internal` is enough for a
member of the test assembly itself, or of an assembly that grants it `InternalsVisibleTo`; a member
of any other referenced assembly has to be `public`.

The same reach decides `[ClassDataSource<T>]` and `[ValuesFrom<T>]`, which are reported separately as
`NU0022` because the type is judged rather than a member: the registry emits `typeof(T)` and `new T()`
for them. The trap is that C# accepts more at the attribute than the registry can name. A `private` or
`protected` nested source satisfies the `IEnumerable` and `new()` constraints where you write the
attribute, and a `protected` source declared on a base class is in scope in your derived test class,
but neither can be named from the registry. Widen the source type to `public` or `internal`, and with
it every type it is nested in and every type argument it names -- a `public Rows<Secret>` is no more
nameable than `Secret` is.

One shape is exempt. A method-level `[ClassDataSource<T>]` on a test whose parameters carry their own
`[Values]`, `[ValuesFromMember]`, or `[ValuesFrom<T>]` expands nothing -- the parameter-level sources
win, which is what `NEXTUNIT010` warns about -- so nothing about `T` reaches the registry: no
`typeof(T)`, no `new T()`, and no trimming root. There is no build error to replace, so `NU0022` says
nothing about it. Remove the parameter-level sources to put the class source back in play and the rule
applies again.

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

The token parameter is only meaningful for a source NextUnit reads asynchronously. A type that
implements `IEnumerable`, generic or not, as well as `IAsyncEnumerable<T>` is read synchronously, so
that a type which meant `IEnumerable` before async sources existed keeps meaning it, and the
synchronous read has no token to pass. A member returning such a type and taking a
`CancellationToken` binds to nothing, and is reported at build time as `NU0021`. Drop the parameter,
or return a type that implements only `IAsyncEnumerable<T>`.

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
dotnet run -- --test-name "Add_*"
```

`--test-name` matches the test's display name, which defaults to the method name. The class name is
not part of it, so `"*CalculatorTests*"` selects nothing; filter by class through
`[Category]` or `[Tag]` instead.

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

### When a run selects no tests

`LifecycleScope.Session` and `LifecycleScope.Assembly` hooks wrap the tests a run selected, so a run
whose filter selects none of them runs neither half: no `[Before(LifecycleScope.Session)]`, and no
`[After(LifecycleScope.Session)]`. NextUnit writes one line to standard error naming how many
`[After(LifecycleScope.Session)]` hooks it skipped, so an empty selection never reads as a session
that tore down cleanly. Assembly-level fixtures in xUnit, NUnit, and MSTest are tied to the selected
tests the same way.

Session-shared data source instances are released either way. Data sources are expanded before the
row-level filter runs, so a run that ends up selecting nothing can still have constructed one, and
nothing else would dispose it.

Once the session is open, `[After(LifecycleScope.Session)]` runs whatever happens next -- including
when a `[Before(LifecycleScope.Session)]` hook throws partway through, because a hook that failed
halfway may already hold what its teardown releases. Session hooks are not inherited, so the scope
has no levels to unwind selectively: every `[After(LifecycleScope.Session)]` hook runs, in reverse
declaration order.

### When a class setup fails

A `[Before(LifecycleScope.Class)]` hook that throws fails that class and only that class. Every test
of the class is reported as failed, once each, carrying the setup exception, and the test bodies do
not run. The setup is not attempted again for the tests that follow it, `[Retry]` included. Every
other class in the assembly still runs, and the run as a whole still ends failed.

The class then cleans up as it does after any failure: its `[After(LifecycleScope.Class)]` hooks run
for the levels the setup reached, and the shared instance is disposed. `Assert.Skip` from a class
setup still skips the class rather than failing it, and cancelling the run still ends the whole run
rather than being reported as one class's failure. A test that `[DependsOn]` one of the failed tests
is reported skipped, exactly as it is when the test it depends on fails for any other reason.

## Inheritance from a base test class

Hooks and configuration attributes declared on a base test class apply to every class derived from
it. This is what xUnit, NUnit, and MSTest all do, and what NextUnit did not do before 3.0.0.

`[Before]` hooks run base class first and then derived; `[After]` hooks unwind in the opposite
order, derived class first and then base. Only the class levels reverse -- several hooks declared in
one class keep their declaration order in both directions.

```csharp
public class DatabaseFixture
{
    [Before(LifecycleScope.Test)]
    public void OpenConnection() { }

    [After(LifecycleScope.Test)]
    public void CloseConnection() { }
}

public class OrderTests : DatabaseFixture
{
    [Before(LifecycleScope.Test)]
    public void SeedOrders() { }

    // OpenConnection, SeedOrders, the test, then CloseConnection.
    [Test]
    public void Reads() { }
}
```

Rules worth knowing before you rely on it:

- **A hook must be `public` or `internal`.** The generated registry calls it from outside your class,
  so a `protected` or `private` hook is reported as `NEXTUNIT015` rather than silently skipped.
- **A hook cannot be an explicit interface implementation.** The registry calls a hook through its
  declaring type, and `void IFixture.Setup()` is reachable only through the interface, so it is
  reported as `NEXTUNIT017`. Implement the member implicitly instead -- a `public void Setup()` that
  satisfies the interface. Unlike the rule above, this one is reported where the hook is declared
  even if nothing in that project derives from the class, because a project that references the
  assembly cannot see the member at all and so could never report it.
- **Overriding a hook replaces it.** The hook runs once, from the base class's position, and the body
  that runs is the most derived override. Overriding it with an empty body is the supported way to
  opt one derived class out of an inherited hook.
- **Hiding a hook with `new` does not replace it.** A `new` method is a different method, so an
  annotated one is a second hook and both run.
- **`LifecycleScope.Class` hooks run once per derived class**, not once for the whole hierarchy.
- **`LifecycleScope.Assembly` and `LifecycleScope.Session` hooks are not inherited.** They already run
  once for the whole run, and running them once per derived class would be a different thing. A base
  class in a *referenced* assembly contributes its Test and Class hooks, but its assembly and session
  hooks belong to the assembly that declared them.
- **Order within one class is declaration order.** Across the parts of a `partial` class, or for a
  base class read from a referenced assembly, the order among that class's own hooks is whatever the
  compiler reports and is not part of the contract. Declare one hook per scope per class if order
  matters.
- **`[After]` hooks run after a failure, but only for the classes that were reached.** A class counts
  as reached as soon as the engine starts its part of the setup, so a `[Before]` that throws halfway
  still runs that class's `[After]` hooks -- what it had already acquired still has to be released.
  Classes further down the chain were never reached, so their `[After]` hooks do not run. If a
  `[Before]` on a base class throws, the derived class's `[After]` hooks are skipped and the base
  class's are not.
- **A `[Timeout]` does not bound an `[After]` hook.** Teardown is passed the run's cancellation token
  rather than the timeout's, because a timed-out test is exactly when cleanup matters and the timeout
  has already fired by then. `TestContext.Current.CancellationToken` still describes the attempt, so
  inside an `[After]` running after a timeout it reads as cancelled while the token the hook is
  handed does not. A hook that can hang needs its own deadline.
- **A failing `[After]` fails the test and ends its attempts.** Its exception is reported alongside the
  test's own, the test's first, and a cleanup failure makes the attempt terminal, so `[Retry]` does not
  run again after one -- exactly as it has always behaved when a disposer throws.
  `IDisposable` or `IAsyncDisposable` on the test class
  is still the stronger guarantee: the engine disposes the instance after every attempt whatever
  happened, including after a teardown hook threw, and a base class's `Dispose` runs for a derived
  instance without any framework involvement.

Attributes follow the same nearest-declaration-wins rule, resolved through the method, then the
method it overrides, then the class, then its base classes, then the assembly where the attribute
allows one:

| Inherited | Not inherited |
| --- | --- |
| `[Timeout]`, `[Retry]`, `[Retry<TPolicy>]`, `[Flaky]` | `[Test]` |
| `[ExecutionPriority]`, `[ParallelLimit]`, `[ParallelGroup]`, `[NotInParallel]` | `[Arguments]`, `[TestData]`, `[ClassDataSource<T>]` |
| `[Culture]`, `[UICulture]`, `[InvariantCulture]` | `[Matrix]`, `[MatrixExclusion]`, `[Values]`, `[ValuesFrom<T>]`, `[ValuesFromMember]` |
| `[Category]`, `[Tag]` (accumulated across every level) | `[Repeat]`, `[DependsOn]`, `[Skip]`, `[DisplayName]` |
| `[Explicit]`, `[DisplayNameFormatter]`, `[DisplayNameFormatter<T>]` | `[Before]`, `[After]` (see the hook rules above) |

The line is that configuration is inherited and the test set is not: an attribute that decides
whether a method is a test, what data it runs with, or how many cases it expands to stays where it
was written. `[Category]` and `[Tag]` accumulate rather than resolve, duplicates included, and
nothing removes an inherited one -- move the attribute down to the classes that want it.

`[Test]` itself is not inherited, so an override that does not carry `[Test]` is not discovered as a
separate test; the test belongs to the class that declares the attribute.

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

`[Skip]` keeps a test out of the run entirely, and the reason travels to the result:

```csharp
// Skip with reason
[Test]
[Skip("Waiting for bug fix #123")]
public void PendingTest() { }
```

When only the running test can decide, skip from inside it. `Assert.Skip` always skips,
`Assert.SkipWhen` skips when the condition holds, and `Assert.SkipUnless` skips when it does not:

```csharp
[Test]
public void ReadsFromTheDatabase()
{
    Assert.SkipUnless(
        Environment.GetEnvironmentVariable("DB_CONNECTION") is not null,
        "DB_CONNECTION is not configured");

    // Reaches here only when the connection string is present.
}
```

`Assert.SkipOnWindows`, `Assert.SkipOnLinux`, `Assert.SkipOnMacOS`, and `Assert.SkipOnFreeBSD` are
shorthands for the platform case. Each takes an optional reason and falls back to a default message.

The two forms differ in more than syntax. A runtime skip starts the test, so the constructor,
the test-scoped `[Before]` hooks, and everything before the skip call have already run, and any
artifacts collected up to that point are kept. The result is reported as skipped rather than failed,
and it is never retried: `[Retry]` ends the sequence on a runtime skip the same way it ends on a
pass.

## Retrying Failed Tests

`[Retry(count)]` runs a failing test again until it passes or the attempt budget is spent. The count
includes the first attempt, so `[Retry(3)]` means at most three runs. An optional second argument
adds a delay in milliseconds between attempts.

```csharp
public class OrderTests
{
    [Test]
    [Retry(3)]
    public async Task PlacesOrder() { }

    [Test]
    [Retry(3, 200)]  // Wait 200ms before each retry
    public async Task PlacesOrderWithBackoff() { }
}

// A class-level budget applies to every test in the class
[Retry(2)]
public class FlakyIntegrationTests
{
    [Test]
    public async Task ReadsFromTheApi() { }

    [Test]
    [Retry(4)]  // Method level replaces the class declaration entirely
    public async Task ReadsFromTheSlowApi() { }
}
```

Timeouts, runtime skips, and run cancellation are never retried, and neither is a failure that came
from cleaning up after the test rather than from the test: an `[After]` hook that threw, or a
`Dispose` that threw, ends the attempts where it happened. A `[Timeout]` budget applies to each
attempt separately, not to the whole retry sequence. A count below 1 is reported as `NU0017`.

### Retrying Selectively

By default every other failure is retried. To decide per failure, implement `IRetryPolicy` and attach
it with `[Retry<TPolicy>(count)]`:

```csharp
using NextUnit;

public sealed class RetryTransientFailures : IRetryPolicy
{
    public ValueTask<bool> ShouldRetryAsync(RetryContext context) =>
        ValueTask.FromResult(context.Exception is HttpRequestException or TimeoutException);
}

public class ApiTests
{
    [Test]
    [Retry<RetryTransientFailures>(3)]
    public async Task ReadsFromTheApi() { }
}
```

`RetryContext` carries the failure and everything needed to judge it:

| Member | Meaning |
| ------ | ------- |
| `Exception` | The exception that failed the attempt |
| `TestContext` | The `ITestContext` of the attempt, including `StateBag` and `Output` |
| `Attempt` | The one-based number of the attempt that just failed |
| `MaxAttempts` | The total budget, so a policy can see how much is left |
| `CancellationToken` | The run token, for a policy that waits or probes before deciding |

The decision is asynchronous, so a policy may consult a health endpoint or wait for a dependency
before answering. Rules worth knowing:

- The policy is consulted only when another attempt is available. It never runs after the last
  attempt, and never for a passing, skipped, timed-out, or cancelled attempt.
- One policy instance is created per test execution, on the first decision, and reused for the
  remaining decisions of that test. Instances are never shared between tests and are never disposed.
- A policy that throws does not silently mean "yes" or "no": the test's own failure and the policy
  failure are reported together, and no further attempt runs.
- The policy type needs a public parameterless constructor. The `new()` constraint on
  `[Retry<TPolicy>]` makes the compiler enforce that, and the generator emits a direct constructor
  call so policies work under Native AOT without reflection.
- Because the constructor call is direct, the policy must be visible from the generated registry:
  `internal` or `public`, and not nested inside a private or protected scope. A policy that is not is
  reported as `NU0016`.
- Applying both `[Retry]` and `[Retry<TPolicy>]` to the same method or class is reported as `NU0015`.

### Observing Attempts

`ITestContext.RetryAttempt` is the one-based number of the attempt currently running. It is 1 on the
first attempt of every test, whether or not `[Retry]` is applied:

```csharp
[Test]
[Retry(3)]
public async Task ReadsFromTheApi()
{
    TestContext.Current!.Output.WriteLine($"Attempt {TestContext.Current.RetryAttempt}");
}
```

When a retried test ultimately fails, its output ends with a line recording how many attempts ran:

```text
[NextUnit] Test failed after 2 of 5 attempts.
```

Attempts run, not attempts budgeted, so this shows when a policy stopped the sequence early. Every
way a retried test can end in failure carries it: an exhausted budget, a policy that declined, a
policy that threw, a timeout, and a failing `Dispose`. A test that eventually passed does not, and
neither does a runtime skip. NextUnit keeps no separate retry statistics; the attempt count reaches
you through the ordinary test result.

Each attempt is a complete test execution. A new test class instance is created and disposed, the
test-scoped `[Before]` hooks run again, and the context is rebuilt, so `StateBag` entries, captured
output, and attached artifacts do not carry over from a discarded attempt. Only the final attempt's
output and artifacts are reported. State that must survive a retry belongs in a static field or in
class-scope state.

## Controlling the Culture

Tests that format or parse dates, numbers, or currency depend on `CultureInfo.CurrentCulture`, so
they pass on one machine and fail on another. `[Culture]` pins the culture a test runs under, and
`[UICulture]` pins the culture used to look up localized resources.

```csharp
public class FormattingTests
{
    [Test]
    [Culture("de-DE")]
    public void ParsesTheGermanDecimalSeparator()
    {
        Assert.Equal(1234.5, double.Parse("1234,5"));
    }

    [Test]
    [UICulture("ja-JP")]
    public void LooksUpJapaneseResources() { }
}
```

`[InvariantCulture]` is shorthand for setting both to the invariant culture, which is the usual way
to make a test independent of the machine it runs on:

```csharp
[InvariantCulture]
public class InvariantFormattingTests
{
    [Test]
    public void FormatsTheSameEverywhere()
    {
        Assert.Equal("1234.5", 1234.5.ToString());
    }
}
```

All three attributes apply at assembly, class, and method level. Each axis resolves on its own and
the most specific declaration wins, so a method can override the culture while inheriting the UI
culture from its class or assembly:

```csharp
[assembly: UICulture("fr-FR")]

[Culture("de-DE")]
public class MixedTests
{
    [Test]
    [Culture("ja-JP")]  // ja-JP formatting, fr-FR resources
    public void Mixed() { }
}
```

Because `[InvariantCulture]` only supplies the axes a level leaves unspecified, combining it with an
explicit attribute is a composition rather than a conflict: `[InvariantCulture]` with
`[UICulture("ja-JP")]` means invariant formatting with Japanese resources.

The culture covers the whole test attempt -- constructor, test-scoped `[Before]` and `[After]` hooks,
the test method, and disposal -- and is applied again for each `[Retry]` attempt, so an attempt that
changes the culture cannot decide what the next one starts from. It is set inside the test's own
asynchronous flow, so tests running in parallel each see their own culture and never each other's,
and the previous culture is put back after a pass, a failure, a timeout, and a cancellation alike.

Two limits are worth knowing. Display names are built during discovery rather than execution, so a
declared culture does not change them. And a name that matches no culture on the machine running the
test fails that test with a message naming it; only names that no machine could accept, such as one
containing a space, are rejected at build time as `NU0018`.

> **Coming from NUnit**: `[Culture]` here does what NUnit's `[SetCulture]` does. NUnit's own
> `[Culture]` attribute *filters* which tests run, which NextUnit has no equivalent for.

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
