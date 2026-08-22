# Migrating from NUnit to NextUnit

This guide maps NUnit concepts onto NextUnit: project setup, lifecycle, data sources, filtering,
assertions, and the places where NextUnit deliberately has no equivalent. Read it top to bottom the
first time, then use the tables as a reference.

This guide, like the rest of the documentation on `main`, describes NextUnit as it stands there while
pinning the latest released version, so between releases it can mention an API the pinned version
does not ship yet; to read it as it stood for an earlier release, check out that release's git tag.

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
| `[TestCase]` | `[Arguments]` | `ExpectedResult` has no equivalent; see [Inline rows](#inline-rows) |
| `[TestCaseSource]` | `[TestData]`, `[ClassDataSource<T>]` | |
| `TestCaseData` row metadata | `TestDataRow<T>` | |
| `[Values]` | `[Values]` | Cartesian product by default, as in NUnit, but the parameterless form needs values spelled out |
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
| `[Retry]` | `[Retry]`, `[Retry<TPolicy>]` | Retries a wider set of failures; see [Retry, repeat, timeout, and culture](#retry-repeat-timeout-and-culture) |
| `[Repeat]` | `[Repeat]` | Repetitions become independent test cases; see [Retry, repeat, timeout, and culture](#retry-repeat-timeout-and-culture) |
| `[Order]` | `[ExecutionPriority]` | Higher runs first, the opposite of `[Order]` |
| `[Parallelizable]` | default behavior | |
| `[NonParallelizable]` | `[NotInParallel]` | |
| `[LevelOfParallelism]` | `[ParallelLimit]` | Applies at assembly, class, or method level; see [Parallelism and ordering](#parallelism-and-ordering) |
| `[SetCulture]` | `[Culture]` | |
| `[SetUICulture]` | `[UICulture]` | |
| `[Culture]` (filter) | none | NUnit's `[Culture]` selects tests; NextUnit's sets one |
| `[Platform(Exclude = "Win")]` | `Assert.SkipOnWindows`, `SkipOnLinux`, `SkipOnMacOS`, `SkipOnFreeBSD` | The skip helpers exclude one platform |
| `[Platform("Win")]` | `Assert.SkipUnless(OperatingSystem.IsWindows(), reason)` | Include needs the predicate form; see [Filtering and selection](#filtering-and-selection) |
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
    <PackageReference Include="NextUnit" Version="4.0.0" />
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

`LifecycleScope` has four values you will use: `Test`, `Class`, `Assembly`, and `Session`. Session
scope is the outermost: it wraps the whole run under Microsoft.Testing.Platform.

`[SetUpFixture]` is the one mapping that loses precision. NUnit scopes it to the namespace it is
declared in and that namespace's descendants, and NextUnit has no namespace scope, so an
assembly-scoped hook runs for every test in the assembly instead. Use it only for setup that is
genuinely assembly-wide. When two namespaces each had their own fixture, collapsing both into
assembly scope makes each one run for the other's tests: move the setup into class-scoped hooks on
the classes that need it, or split the namespaces into separate test projects.

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
converts directly.

Class-, assembly-, and session-scoped hooks are declared `static` in the examples above, and that is
not a style choice. NextUnit builds one instance of the class to run the class-scoped hooks on, and a
separate instance for each test, so an instance `[Before(LifecycleScope.Class)]` compiles and runs but
writes its fields on an object no test ever sees. An NUnit `[OneTimeSetUp]` that populates instance
fields therefore needs its state moved as well as its attribute changed: make the field `static`, so
that the hook and the tests reach the same object.

```csharp
using NextUnit;

public class SchemaTests
{
    private static string? _connectionString;

    [Before(LifecycleScope.Class)]
    public static void CreateSchema() => _connectionString = "server=localhost";

    [After(LifecycleScope.Class)]
    public static void DropSchema() => _connectionString = null;

    [Test]
    public void ReadsTheSchema() => Assert.NotNull(_connectionString);
}
```

That `static` field is reachable from several tests at once, because NUnit runs tests serially unless
you opt in and NextUnit runs them in parallel unless you opt out. Add `[NotInParallel]` to the class
when the shared object cannot take concurrent use; see
[Parallelism and ordering](#parallelism-and-ordering).

Hooks are inherited, as they are in NUnit. A `[Before]` on an abstract base class runs for the
derived classes that hold the tests, base class first, and `[After]` unwinds derived class first.
The hook has to be `public` or `internal`, because the generated registry calls it from outside your
class; a `protected` one -- common in NUnit base fixtures -- is reported as `NEXTUNIT015` rather than
silently skipped. Configuration attributes such as `[Timeout]` and `[Category]` are inherited on the
same rule. See
[Inheritance from a base test class](GETTING_STARTED.md#inheritance-from-a-base-test-class).

One difference from NUnit to plan for. NextUnit tears down only the classes whose setup it reached,
which is NUnit's rule, but a `[Timeout]` does not bound an `[After]` hook: teardown is passed the
run's cancellation token rather than the timeout's, so a hook that can hang needs its own deadline.
And where NUnit leaves the order of several `[SetUp]` methods at one level
unspecified, NextUnit runs them in declaration order -- except across the parts of a `partial` class,
where the order is not part of the contract either.

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

`ExpectedResult` needs care, because nothing warns you when it is lost. NUnit compares the returned
value against the expected one; NextUnit awaits a test's return value and discards it, so a method
migrated as-is passes whatever it returns. Carry the expectation across as one more argument and
assert it in the body:

```csharp nunit
using NUnit.Framework;

public class AdditionTests
{
    [TestCase(1, 2, ExpectedResult = 3)]
    [TestCase(5, 5, ExpectedResult = 10)]
    public int Add(int a, int b) => a + b;
}
```

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

The same applies to a `[TestCaseSource]` whose rows use `TestCaseData.Returns`: the expected value
becomes an ordinary column of the row.

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
control how often the data source type is instantiated. The instance is enumerated for its rows
rather than handed to the test, so state the test body itself needs belongs in a `static` field with
[lifecycle hooks](#lifecycle), not in a data source. The scope spans attributes: a type used by both
`[ClassDataSource<T>]` and `[ValuesFrom<T>]` under the same scope is one instance. A shared instance
is disposed at the end of the session, after the `[After(LifecycleScope.Session)]` hooks, and
`IAsyncDisposable` is preferred over `IDisposable` when a data source implements both.

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

Spell the values out. NUnit fills in the cases for a parameterless `[Values]` on a `bool` or an enum
parameter; NextUnit's `[Values]` stores exactly what you pass, and a parameter with no values
contributes nothing to the product, so the whole method expands to zero test cases and disappears
from the run without failing. Write `[Values(false, true)]`, or point `[ValuesFromMember]` at
`Enum.GetValues<T>()`.

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
| `Assert.Throws<T>(() => ...)` | `Assert.Throws<T>(() => ...)` (matches subtypes too) |
| `Assert.ThrowsAsync<T>(async () => ...)` | `await Assert.ThrowsAsync<T>(async () => ...)` |
| `Assert.Fail(message)` | `Assert.Fail(message)` |
| `Assert.Ignore(reason)` | `Assert.Skip(reason)` |

The classic NUnit forms -- `Assert.AreEqual`, `Assert.IsTrue`, `Assert.IsNull` -- keep their argument
order, so those rewrites are pure renames.

`Assert.Throws<T>` matches more widely than NUnit's does. NextUnit catches `TException`, so a subtype
satisfies it: `Assert.Throws<ArgumentException>` accepts an `ArgumentNullException`. NUnit's
`Assert.Throws<T>` requires the exact type and has `Assert.Catch<T>` for the assignable case. A
migrated test can therefore pass on an exception it used to reject. Where the exact type is the point
of the test, assert it on the returned exception:

```csharp
using NextUnit;

public class ExactTypeTests
{
    [Test]
    public void RejectsASubtype()
    {
        var error = Assert.Throws<ArgumentException>(() => throw new ArgumentException("bad"));
        Assert.Equal(typeof(ArgumentException), error.GetType());
    }
}
```

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
`Assert.SkipUnless` cover the runtime form.

Platform selection needs care, because `[Platform]` includes by default and the skip helpers exclude.
`Assert.SkipOnWindows`, `SkipOnLinux`, `SkipOnMacOS`, and `SkipOnFreeBSD` replace
`[Platform(Exclude = "Win")]` and its siblings. An include such as `[Platform("Win")]` means the test
runs *only* on Windows, so the same rewrite would invert it -- the test would then skip on Windows
and run everywhere else. Express an include with the predicate form instead:

```csharp
using NextUnit;

public class RegistryTests
{
    [Test]
    public void ReadsTheRegistry()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "The registry exists only on Windows");
        Assert.True(OperatingSystem.IsWindows());
    }
}
```

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
other without serializing the whole run.

`[ParallelLimit]` resolves from the test method, then its containing class, then the assembly, so an
assembly-wide `[LevelOfParallelism(4)]` maps to `[assembly: ParallelLimit(4)]`. A test that no level
declares a limit for is bounded by the processor count, except when it is scheduled alongside a
limit-declaring test that shares its `[ParallelGroup]`: that batch runs at the smallest limit
declared within it, even when that is larger than the processor count. `[Timeout]` and the culture
attributes resolve across the same three levels.

The two are not exact equivalents. `[LevelOfParallelism]` is an assembly-wide maximum, while the
NextUnit value is the default a test inherits when neither its method nor its class declares one --
and the nearest declaration replaces it with a larger value as readily as with a smaller one, so a
class declaring `[ParallelLimit(8)]` runs 8. Porting a limit that protects a shared resource means
auditing the class-level and method-level declarations too.

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

`[Retry(count)]` counts the first attempt, so `[Retry(3)]` means at most three runs, matching the
convention of NUnit's `tryCount`. The optional second argument is a delay in milliseconds between
attempts. A `[Timeout]` budget applies to each attempt separately, and
timeouts, runtime skips, and cancellation are never retried.

`[Repeat]` changes shape rather than meaning. NUnit runs the repetitions in sequence and stops at the
first failure, so one repeated test produces one result. NextUnit expands them into independent test
cases, each with its own id and result -- at build time, or at discovery when the test also carries
parameter-level data sources whose length is not known until then -- and the scheduler may run them
in parallel with everything else. A repeated test that mutates shared state can therefore overlap with itself,
and a failing repetition no longer stops the ones after it. Add `[NotInParallel]` when the overlap is
the problem, and write an ordinary loop inside one test when you need stop-on-first-failure.

The retryable set is wider here than in NUnit. NextUnit's `[Retry]` re-runs a test after any failure
except a timeout, a runtime skip, cancellation, and a failure thrown while cleaning up after the test
rather than by the test -- an `[After]` hook or a `Dispose` -- including one that threw an unexpected
exception.
NUnit's `[Retry]` re-runs an assertion failure and leaves an error alone, so a test that used to fail
at once can now be retried into passing. `IRetryPolicy`, attached with `[Retry<TPolicy>(count)]`,
decides per failure. A policy that accepts only `AssertionFailedException` is the closest match to
what NUnit retried:

```csharp
using NextUnit;

public sealed class RetryAssertionFailures : IRetryPolicy
{
    public ValueTask<bool> ShouldRetryAsync(RetryContext context) =>
        ValueTask.FromResult(context.Exception is AssertionFailedException);
}

public class PaymentTests
{
    [Test]
    [Retry<RetryAssertionFailures>(3)]
    public Task ChargesTheCardAsync() => Task.CompletedTask;
}
```

Retrying an assertion failure is rarely what you want once the suite is migrated, though: a flaky
integration test usually fails on the transport rather than the assertion. A policy matching
`HttpRequestException or TimeoutException` targets that directly, and is worth reaching for instead of
reproducing NUnit's split.

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
| `[TestFixture(args)]`, `[TestFixtureSource]` | Test identity is generated per method, not per fixture instance | `[Arguments]`, `[TestData]`, or `[ClassDataSource<T>]` on the method |
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
