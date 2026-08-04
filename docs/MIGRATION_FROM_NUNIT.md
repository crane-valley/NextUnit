# Migrating from NUnit to NextUnit

This guide maps NUnit concepts onto NextUnit: project setup, lifecycle, data sources, filtering,
assertions, and the places where NextUnit deliberately has no equivalent. Read it top to bottom the
first time, then use the tables as a reference.

Two conventions apply throughout. Blocks labelled NUnit show the code you are migrating away from;
every NextUnit block is compiled in CI, so what you see is what the compiler accepts.

## Attribute reference

| NUnit | NextUnit | Notes |
| ----- | -------- | ----- |
| `[Test]` | `[Test]` | Same name, same meaning |
| `[TestFixture]` | none needed | Any public class with `[Test]` methods is discovered |
| `[TestFixture(args)]`, `[TestFixtureSource]` | none | Parameterize the test method instead |
| `[SetUp]` | `[Before(LifecycleScope.Test)]` | |
| `[TearDown]` | `[After(LifecycleScope.Test)]` | |
| `[OneTimeSetUp]` | `[Before(LifecycleScope.Class)]` | |
| `[OneTimeTearDown]` | `[After(LifecycleScope.Class)]` | |
| `[SetUpFixture]` | `[Before(LifecycleScope.Assembly)]`, `LifecycleScope.Session` | |
| `[TestCase]` | `[Arguments]` | |
| `[TestCaseSource]` | `[TestData]`, `[ClassDataSource<T>]` | |
| `TestCaseData` row metadata | `TestDataRow<T>` | |
| `[Values]` | `[Values]` | Cartesian product by default, as in NUnit |
| `[ValueSource]` | `[ValuesFromMember]`, `[ValuesFrom<T>]` | |
| `[Combinatorial]` | default behavior, `[Matrix]` | |
| `[Sequential]`, `[Pairwise]` | none | Write the rows out with `[Arguments]` or `[TestData]` |
| `[Range]` | `[ValuesFromMember]` over `Enumerable.Range` | |
| `[Random]` | none | Generate values in a `[TestData]` member you control |
| `[Category]` | `[Category]` | |
| `[Property]`, `[Author]` | `[Tag]` | `[Tag]` carries a name, not a name/value pair |
| `[Description]` | `[DisplayName]` | Changes the reported name; there is no separate description |
| `[Explicit]` | `[Explicit]` | |
| `[Ignore]` | `[Skip]` | |
| `Assert.Ignore` | `Assert.Skip` | |
| `[Timeout]` | `[Timeout]` | |
| `[MaxTime]` | none | Assert on your own stopwatch, or use `[Timeout]` |
| `[Retry]` | `[Retry]`, `[Retry<TPolicy>]` | |
| `[Repeat]` | `[Repeat]` | |
| `[Order]` | `[ExecutionPriority]` | Higher runs first, the opposite of `[Order]` |
| `[Parallelizable]` | default behavior | |
| `[NonParallelizable]` | `[NotInParallel]` | |
| `[LevelOfParallelism]` | `[ParallelLimit]` | Applies at assembly, class, or method level |
| `[SetCulture]` | `[Culture]` | |
| `[SetUICulture]` | `[UICulture]` | |
| `[Culture]` (filter) | none | NUnit's `[Culture]` selects tests; NextUnit's sets one |
| `[Platform]` | `Assert.SkipOnWindows`, `SkipOnLinux`, `SkipOnMacOS`, `SkipOnFreeBSD` | |
| `Assert.That` and constraints | `Assert.Equal`, `Assert.True`, and friends | See [Assertions](#assertions) |
| `Assert.Multiple` | none | See [Deliberate non-equivalents](#deliberate-non-equivalents) |
| `TestContext.CurrentContext` | `TestContext.Current` | |
| `TestContext.WriteLine` | `ITestContext.Output.WriteLine`, injected `ITestOutput` | |
| `TestContext.AddTestAttachment` | `ITestContext.AttachArtifact` | |

## Project setup

Remove the NUnit packages and add the single NextUnit package.

```bash
dotnet remove package NUnit
dotnet remove package NUnit3TestAdapter
dotnet remove package Microsoft.NET.Test.Sdk
dotnet add package NextUnit
```

The project file loses the runner plumbing. Before:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NUnit" Version="4.3.1" />
    <PackageReference Include="NUnit3TestAdapter" Version="6.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.5.1" />
  </ItemGroup>
</Project>
```

After:

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

The `NextUnit` package brings the runtime, the source generator, the analyzers, the
Microsoft.Testing.Platform integration, and TRX reporting. You do not add `OutputType`, a
`Program.cs`, or a separate analyzer reference.

To run a whole repository with `dotnet test` on the .NET 10 SDK, select Microsoft.Testing.Platform in
`global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

`dotnet run --project MyProject.Tests` needs no repository-level configuration.

For a new project rather than a migrated one, the template package produces the layout above with one
passing test:

```bash
dotnet new install NextUnit.Templates
dotnet new nextunit -n MyProject.Tests
```

## Writing a test

NUnit:

```csharp nunit
using NUnit.Framework;

[TestFixture]
public class CalculatorTests
{
    [Test]
    public void Add_ReturnsSum()
    {
        Assert.That(2 + 2, Is.EqualTo(4));
    }
}
```

NextUnit:

```csharp
using NextUnit;

public class CalculatorTests
{
    [Test]
    public void Add_ReturnsSum()
    {
        Assert.Equal(4, 2 + 2);
    }
}
```

Two differences matter. `[TestFixture]` disappears, because a public class holding `[Test]` methods is
already a fixture. And the argument order flips: NUnit's constraint form puts the actual value first,
while NextUnit puts the expected value first, as `Assert.AreEqual` and xUnit do. Reversing a pair
still compiles, so it is the mistake to watch for during a bulk rewrite.

There is no `using` for NextUnit in `ImplicitUsings`, so add `using NextUnit;` to every test file, or
a `global using NextUnit;` once per project.

## Lifecycle

NUnit spreads setup across four attributes. NextUnit uses two attributes and a scope.

NUnit:

```csharp nunit
using NUnit.Framework;

[TestFixture]
public class OrderTests
{
    [OneTimeSetUp]
    public void CreateSchema() { }

    [SetUp]
    public void ResetRows() { }

    [TearDown]
    public void ClearRows() { }

    [OneTimeTearDown]
    public void DropSchema() { }

    [Test]
    public void PlacesAnOrder() { }
}
```

NextUnit:

```csharp
using NextUnit;

public class OrderTests
{
    [Before(LifecycleScope.Class)]
    public static void CreateSchema() { }

    [Before(LifecycleScope.Test)]
    public void ResetRows() { }

    [After(LifecycleScope.Test)]
    public void ClearRows() { }

    [After(LifecycleScope.Class)]
    public static void DropSchema() { }

    [Test]
    public void PlacesAnOrder() { }
}
```

`LifecycleScope` has four values you will use: `Test`, `Class`, `Assembly`, and `Session`. An
`[SetUpFixture]` that prepares a namespace becomes an assembly- or session-scoped hook. Session scope
is the outermost: it wraps the whole run under Microsoft.Testing.Platform.

```csharp
using NextUnit;

public class ContainerTests
{
    [Before(LifecycleScope.Assembly)]
    public static Task StartContainersAsync() => Task.CompletedTask;

    [After(LifecycleScope.Assembly)]
    public static Task StopContainersAsync() => Task.CompletedTask;

    [Test]
    public void TalksToTheContainer() { }
}
```

Hooks may return `void`, `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>`, so NUnit's async setup
converts directly. Class-, assembly-, and session-scoped hooks are declared `static` in the examples
above because they run once, outside any single test instance.

## Data sources

### Inline rows

NUnit's `[TestCase]` becomes `[Arguments]`, one attribute per row.

NUnit:

```csharp nunit
using NUnit.Framework;

public class AdditionTests
{
    [TestCase(1, 2, 3)]
    [TestCase(5, 5, 10)]
    public void Add(int a, int b, int expected)
    {
        Assert.That(a + b, Is.EqualTo(expected));
    }
}
```

NextUnit:

```csharp
using NextUnit;

public class AdditionTests
{
    [Test]
    [Arguments(1, 2, 3)]
    [Arguments(5, 5, 10)]
    public void Add(int a, int b, int expected) => Assert.Equal(expected, a + b);
}
```

`[Test]` is required alongside `[Arguments]`. NUnit infers the test from `[TestCase]` alone; NextUnit
does not, and reports a data-source attribute without `[Test]` as `NU0013` at build time.

### Member sources

`[TestCaseSource]` becomes `[TestData]`, naming a static member that returns rows.

```csharp
using NextUnit;

public class DiscountTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return [100m, 0.1m, 90m];
        yield return [50m, 0.5m, 25m];
    }

    [Test]
    [TestData(nameof(Cases))]
    public void AppliesDiscount(decimal price, decimal rate, decimal expected) =>
        Assert.Equal(expected, price - (price * rate));
}
```

Use `MemberType` when the source lives on another type: `[TestData(nameof(Cases), MemberType =
typeof(SharedCases))]`.

### Per-row metadata

NUnit attaches a name, category, or ignore reason to a row through `TestCaseData`. NextUnit uses
`TestDataRow<T>`, which is typed, so the compiler checks the row shape against the method signature.
A tuple is spread across the parameters without changing the method.

```csharp
using NextUnit;

public class ShippingTests
{
    public static IEnumerable<TestDataRow<(string Country, decimal Cost)>> Rows()
    {
        yield return new(("JP", 500m), displayName: "domestic");
        yield return new(("US", 2000m), displayName: "international", categories: ["Slow"]);
        yield return new(("BR", 0m), displayName: "unpriced", skipReason: "No rate card yet");
    }

    [Test]
    [TestData(nameof(Rows))]
    public void CalculatesCost(string country, decimal cost) => Assert.NotNull(country);
}
```

The display name, categories, tags, and skip reason apply to that generated test case alone, and
class- or method-level categories are merged with the row's own.

### Class sources

A `[TestCaseSource(typeof(T))]` that points at a type becomes `[ClassDataSource<T>]`. The type
enumerates rows, and `Shared` controls how many instances exist.

```csharp
using NextUnit;

public class MultiplicationData : IEnumerable<object?[]>
{
    public IEnumerator<object?[]> GetEnumerator()
    {
        yield return [2, 3, 6];
        yield return [4, 5, 20];
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

public class MultiplicationTests
{
    [Test]
    [ClassDataSource<MultiplicationData>]
    public void Multiplies(int a, int b, int expected) => Assert.Equal(expected, a * b);

    [Test]
    [ClassDataSource<MultiplicationData>(Shared = SharedType.PerClass)]
    public void MultipliesAgain(int a, int b, int expected) => Assert.Equal(expected, a * b);
}
```

`SharedType` offers `None`, `PerClass`, `PerAssembly`, `PerSession`, and `Keyed` with a `Key`, which
covers the fixture-sharing patterns NUnit expresses through static state.

### Combinatorial parameters

NUnit's `[Values]` on parameters produces a Cartesian product under `[Combinatorial]`, which is the
default. NextUnit behaves the same way, and `[Matrix]` is the equivalent spelling when you want the
combinatorial intent to be explicit.

```csharp
using NextUnit;

public class RenderingTests
{
    public static IEnumerable<int> Heights() => Enumerable.Range(1, 3);

    [Test]
    public void RendersEverywhere(
        [Values("chrome", "firefox")] string browser,
        [ValuesFromMember(nameof(Heights))] int height) => Assert.InRange(height, 1, 3);

    [Test]
    [MatrixExclusion(1, 20)]
    public void SkipsOneCombination(
        [Matrix(1, 2)] int width,
        [Matrix(10, 20)] int height) => Assert.False(width == 1 && height == 20);
}
```

`[Sequential]` and `[Pairwise]` have no equivalent. Write the combinations you want as `[Arguments]`
rows or as a `[TestData]` member, which makes the intended pairing visible instead of implied by an
attribute.

### Asynchronous and deferred sources

A `[TestData]` member may return `IAsyncEnumerable<T>`, `Task<TCollection>`, or
`ValueTask<TCollection>`. Rows are still enumerated once during discovery, so each row stays an
individually selectable test case.

```csharp
using NextUnit;

public class CatalogTests
{
    public static async Task<IEnumerable<object[]>> LoadRowsAsync()
    {
        await Task.Yield();
        return [[1, 1], [2, 4]];
    }

    [Test]
    [TestData(nameof(LoadRowsAsync))]
    public void Squares(int input, int expected) => Assert.Equal(expected, input * input);
}
```

When a source is large or slow enough that reading it at startup is itself the problem, set
`DeferredEnumeration = true` to move the read into the run.

```csharp
using NextUnit;

public class FixtureFileTests
{
    public static IEnumerable<object[]> EveryRow()
    {
        foreach (var line in File.ReadLines("rows.csv"))
        {
            yield return [line];
        }
    }

    [Test]
    [TestData(nameof(EveryRow), DeferredEnumeration = true)]
    public void Parses(string line) => Assert.NotNull(line);
}
```

Deferral costs you per-row selection: discovery reports one placeholder instead of one test per row,
so an IDE cannot run a single row and filters apply to the group. Keep the default eager enumeration
unless startup cost is the problem you are solving.

## Assertions

NextUnit has no constraint model. Every assertion is a static method whose first argument is the
expected value.

| NUnit | NextUnit |
| ----- | -------- |
| `Assert.That(actual, Is.EqualTo(expected))` | `Assert.Equal(expected, actual)` |
| `Assert.That(actual, Is.Not.EqualTo(other))` | `Assert.NotEqual(other, actual)` |
| `Assert.That(actual, Is.EqualTo(expected).Within(0.01))` | `Assert.Equal(expected, actual, tolerance: 0.01)` |
| `Assert.That(value, Is.True)` | `Assert.True(value)` |
| `Assert.That(value, Is.Null)` | `Assert.Null(value)` |
| `Assert.That(actual, Is.SameAs(expected))` | `Assert.Same(expected, actual)` |
| `Assert.That(value, Is.InRange(min, max))` | `Assert.InRange(value, min, max)` |
| `Assert.That(items, Is.Empty)` | `Assert.Empty(items)` |
| `Assert.That(items, Has.Member(item))` | `Assert.Contains(item, items)` |
| `Assert.That(items, Is.EquivalentTo(other))` | `Assert.Equivalent(other, items)` |
| `Assert.That(items, Is.SubsetOf(other))` | `Assert.Subset(items, other)` |
| `Assert.That(text, Does.StartWith(prefix))` | `Assert.StartsWith(prefix, text)` |
| `Assert.That(text, Does.Contain(part))` | `Assert.Contains(part, text)` |
| `Assert.Throws<T>(() => ...)` | `Assert.Throws<T>(() => ...)` |
| `Assert.ThrowsAsync<T>(async () => ...)` | `await Assert.ThrowsAsync<T>(async () => ...)` |
| `Assert.Fail(message)` | `Assert.Fail(message)` |
| `Assert.Ignore(reason)` | `Assert.Skip(reason)` |

The classic NUnit forms -- `Assert.AreEqual`, `Assert.IsTrue`, `Assert.IsNull` -- keep their argument
order, so those rewrites are pure renames.

`Assert.Throws<T>` returns the exception, which is how you assert on its message. The async form
returns a `Task<TException>` rather than the exception itself, so it has to be awaited: an
un-awaited call never observes the failure, and the test passes whatever the delegate does.

```csharp
using NextUnit;

public class ParsingTests
{
    [Test]
    public void RejectsNonNumericInput()
    {
        var error = Assert.Throws<FormatException>(() => int.Parse("abc"));
        Assert.Contains("abc", error.Message);
    }

    [Test]
    public async Task RejectsNonNumericInputAsync()
    {
        var error = await Assert.ThrowsAsync<FormatException>(async () =>
        {
            await Task.Yield();
            int.Parse("abc");
        });

        Assert.Contains("abc", error.Message);
    }
}
```

The second string argument of `Assert.Throws` is a custom failure message, not an expected exception
message. Assert on the returned exception instead.

## Filtering and selection

`[Category]` carries over unchanged, and `[Tag]` adds a second axis for the name/value metadata NUnit
expresses with `[Property]`.

```csharp
using NextUnit;

[Category("Integration")]
public class DatabaseTests
{
    [Test]
    [Tag("Slow")]
    public void ReadsRows() { }

    [Test]
    public void WritesRows() { }
}
```

NUnit's `--where` expression language has no equivalent. NextUnit exposes one option per axis, and
each option may be repeated:

```bash
dotnet run -- --category Integration
dotnet run -- --exclude-tag Slow
dotnet run -- --test-name "*Database*"
dotnet run -- --test-name-regex "Reads.*Rows"
dotnet run -- --explicit
```

Every option has an environment variable, which is what a CI job usually sets once:
`NEXTUNIT_INCLUDE_CATEGORIES`, `NEXTUNIT_EXCLUDE_CATEGORIES`, `NEXTUNIT_INCLUDE_TAGS`,
`NEXTUNIT_EXCLUDE_TAGS`, `NEXTUNIT_TEST_NAME`, `NEXTUNIT_TEST_NAME_REGEX`, and
`NEXTUNIT_INCLUDE_EXPLICIT`. The comma-separated environment values are the ambient default, and a
command-line option overrides them for a single run.

Skipping and explicit selection:

```csharp
using NextUnit;

public class MaintenanceTests
{
    [Test]
    [Skip("Blocked on issue #123")]
    public void RebuildsTheIndex() { }

    [Test]
    [Explicit("Destroys the local database")]
    public void ResetsEverything() { }

    [Test]
    public void NeedsADatabase()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("DB") is not null, "DB is not configured");
        Assert.True(true);
    }
}
```

`[Skip]` is the compile-time form of `[Ignore]`. `Assert.Skip`, `Assert.SkipWhen`, and
`Assert.SkipUnless` cover the runtime form, and `Assert.SkipOnWindows`, `SkipOnLinux`, `SkipOnMacOS`,
and `SkipOnFreeBSD` replace `[Platform]`.

## Parallelism and ordering

NUnit runs tests serially unless you opt in. NextUnit runs them in parallel unless you opt out, so
check your fixtures for shared state before the first run.

```csharp
using NextUnit;

[NotInParallel]
public class MigrationTests
{
    [Test]
    public void RunsAlone() { }
}

[ParallelLimit(4)]
public class ThrottledTests
{
    [Test]
    public void RunsWithAtMostThreeSiblings() { }
}

[ParallelGroup("database")]
public class GroupedTests
{
    [Test]
    public void RunsWithItsGroup() { }
}
```

`[NotInParallel]` also accepts constraint keys, so classes that share one resource can exclude each
other without serializing the whole run. `[ParallelLimit]` and `[Timeout]` also apply at assembly
level, so an assembly-wide `[LevelOfParallelism(4)]` becomes `[assembly: ParallelLimit(4)]` rather
than the same attribute repeated on every class.

Ordering is expressed as a dependency or a priority rather than an index.

```csharp
using NextUnit;

public class PipelineTests
{
    [Test]
    [ExecutionPriority(100)]
    public void RunsFirst() { }

    [Test]
    [DependsOn(nameof(RunsFirst))]
    public void RunsAfterTheFirst() { }
}
```

Higher `[ExecutionPriority]` values run first, which is the reverse of NUnit's `[Order]`. Priority
only orders tests at the same dependency level; `[DependsOn]` is the hard constraint, and it takes
`ProceedOnFailure` when a dependent should still run after its prerequisite fails.

## Retry, repeat, timeout, and culture

```csharp
using NextUnit;

public class ApiTests
{
    [Test]
    [Retry(3, 200)]
    [Timeout(5000)]
    public Task ReadsTheApiAsync() => Task.CompletedTask;

    [Test]
    [Repeat(5)]
    public void IsStableAcrossRuns() { }
}
```

`[Retry(count)]` counts the first attempt, as NUnit's does, and the optional second argument is a
delay in milliseconds between attempts. A `[Timeout]` budget applies to each attempt separately, and
timeouts, runtime skips, and cancellation are never retried.

NUnit retries every failure. To decide per failure, implement `IRetryPolicy` and attach it with
`[Retry<TPolicy>(count)]`:

```csharp
using NextUnit;

public sealed class RetryTransientFailures : IRetryPolicy
{
    public ValueTask<bool> ShouldRetryAsync(RetryContext context) =>
        ValueTask.FromResult(context.Exception is HttpRequestException or TimeoutException);
}

public class PaymentTests
{
    [Test]
    [Retry<RetryTransientFailures>(3)]
    public Task ChargesTheCardAsync() => Task.CompletedTask;
}
```

`RetryContext` carries the `Exception`, the `ITestContext` of the attempt, the one-based `Attempt`,
the `MaxAttempts` budget, and the run `CancellationToken`. The policy type needs a public
parameterless constructor and must be visible from the generated registry, so `internal` or `public`
and not nested in a private scope.

`ITestContext.RetryAttempt` reports the attempt currently running, starting at 1.

`[SetCulture]` and `[SetUICulture]` become `[Culture]` and `[UICulture]`, and `[InvariantCulture]`
sets both to the invariant culture:

```csharp
using NextUnit;

public class FormattingTests
{
    [Test]
    [Culture("de-DE")]
    public void ParsesTheGermanDecimalSeparator() => Assert.Equal(1234.5, double.Parse("1234,5"));

    [Test]
    [InvariantCulture]
    public void FormatsTheSameEverywhere() => Assert.Equal("1234.5", 1234.5.ToString());
}
```

All three apply at assembly, class, and method level, each axis resolves on its own, and the most
specific declaration wins. The culture covers the constructor, the test-scoped hooks, the test
method, and disposal, and is reapplied for each retry attempt.

NUnit's `[Culture]` attribute is a filter rather than a setting, and NextUnit has no equivalent for
it. Use `Assert.SkipUnless` on `CultureInfo.CurrentCulture` when a test genuinely cannot run under
another culture.

## Test context, output, and attachments

NUnit reaches the ambient context through `TestContext.CurrentContext`. NextUnit injects
`ITestContext` or `ITestOutput` into the constructor, and `TestContext.Current` is available where
injection is inconvenient.

```csharp
using NextUnit;
using NextUnit.Core;

public class DiagnosticTests
{
    private readonly ITestOutput _output;

    public DiagnosticTests(ITestOutput output) => _output = output;

    [Test]
    public void WritesDiagnostics()
    {
        _output.WriteLine("running {0}", TestContext.Current!.TestName);
        Assert.True(true);
    }
}
```

`TestContext.AddTestAttachment` becomes `ITestContext.AttachArtifact`, which takes a path or an
`Artifact` with a description and MIME type.

```csharp
using NextUnit;
using NextUnit.Core;

public class ScreenshotTests
{
    [Test]
    public void AttachesAScreenshot()
    {
        var path = Path.Join(Path.GetTempPath(), "screenshot.png");
        File.WriteAllBytes(path, []);

        TestContext.Current?.AttachArtifact(path, "Screenshot");
        TestContext.Current?.AttachArtifact(new Artifact
        {
            FilePath = path,
            Description = "Screenshot",
            MimeType = "image/png",
        });

        Assert.True(File.Exists(path));
    }
}
```

`ITestContext` also exposes `TestName`, `ClassName`, `FullyQualifiedName`, `AssemblyName`,
`Arguments`, `Categories`, `Tags`, `RepeatIndex`, `RetryAttempt`, `TimeoutMs`, `CancellationToken`,
and a `StateBag` for passing values between hooks and the test.

## Deliberate non-equivalents

These are absent by design rather than unimplemented. Each entry states what to use instead.

| NUnit feature | Why NextUnit has no equivalent | Use instead |
| ------------- | ------------------------------ | ----------- |
| Constraint model (`Assert.That`, `Is`, `Has`, `Does`) | NextUnit ships one assertion style, matching xUnit, so there is nothing to choose between | The static `Assert` methods in the table above |
| `Assert.Multiple` | Deferred failure aggregation needs its own execution scope and reporting shape | One assertion per behavior, or assert on a projection of the whole object |
| `[TestFixture(args)]`, `[TestFixtureSource]` | Test identity is generated per method, not per fixture instance | `[Arguments]` or `[TestData]` on the method, or `[ClassDataSource<T>]` |
| `[Sequential]`, `[Pairwise]` | Implicit pairing hides which combinations run | Explicit `[Arguments]` rows or a `[TestData]` member |
| `[Culture]` as a filter | Culture is set for a test, not used to select tests | `Assert.SkipUnless` on the ambient culture |
| `[MaxTime]` | A soft duration report is a benchmarking concern, not a pass/fail one | `[Timeout]` for a hard limit, or a benchmark harness |
| `--where` expression language | One option per axis is enough to script and to explain | `--category`, `--tag`, `--test-name`, `--test-name-regex` |
| `[Random]`, `[Range]` on parameters | A generator hidden in an attribute makes failures hard to reproduce | `[ValuesFromMember]` over a member you control |
| `Assert.IsInstanceOf<T>` | Pattern matching already does this, with better failure information from the compiler | `Assert.True(value is T)`, or a cast that documents the intent |

The [project roadmap](../PLANS.md) records what is planned and what is explicitly not planned. If a
missing piece blocks a real migration, open an issue with the workflow it blocks.

## Migration checklist

1. Replace the NUnit packages with `NextUnit`, and select Microsoft.Testing.Platform in `global.json`
   if you run `dotnet test`.
2. Add `using NextUnit;` to each test file, or one `global using NextUnit;`.
3. Delete `[TestFixture]`, and rewrite `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, and
   `[OneTimeTearDown]` as `[Before]` and `[After]` with a scope.
4. Rewrite `[TestCase]` as `[Test]` plus `[Arguments]`, and `[TestCaseSource]` as `[TestData]`.
5. Rewrite `Assert.That` constraints as static assertions, checking the expected/actual order.
6. Audit for shared state, because tests now run in parallel by default.
7. Replace `--where` expressions in CI with `--category`, `--tag`, or the `NEXTUNIT_*` variables.
8. Run the suite and compare the test count against the NUnit run before deleting anything.

## Further reading

- [Getting Started](GETTING_STARTED.md)
- [Migrating from MSTest](MIGRATION_FROM_MSTEST.md)
- [Migrating from xUnit](MIGRATION_FROM_XUNIT.md)
- [Best Practices](BEST_PRACTICES.md)
- Sample tests covering every feature: [samples/NextUnit.SampleTests](../samples/NextUnit.SampleTests)
