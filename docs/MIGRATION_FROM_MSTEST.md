# Migrating from MSTest to NextUnit

This guide maps MSTest concepts onto NextUnit: project setup, lifecycle, data sources, filtering,
assertions, and the places where NextUnit deliberately has no equivalent. Read it top to bottom the
first time, then use the tables as a reference.

Two conventions apply throughout. Blocks labelled MSTest show the code you are migrating away from;
every NextUnit block is compiled in CI, so what you see is what the compiler accepts.

## Attribute reference

| MSTest | NextUnit | Notes |
| ------ | -------- | ----- |
| `[TestMethod]` | `[Test]` | |
| `[TestClass]` | none needed | Any public class with `[Test]` methods is discovered |
| `[DataTestMethod]` | `[Test]` | One attribute covers both cases |
| `[TestInitialize]` | `[Before(LifecycleScope.Test)]` | |
| `[TestCleanup]` | `[After(LifecycleScope.Test)]` | |
| `[ClassInitialize]` | `[Before(LifecycleScope.Class)]` | No `TestContext` parameter required |
| `[ClassCleanup]` | `[After(LifecycleScope.Class)]` | |
| `[AssemblyInitialize]` | `[Before(LifecycleScope.Assembly)]` | `LifecycleScope.Session` wraps the whole run |
| `[AssemblyCleanup]` | `[After(LifecycleScope.Assembly)]` | |
| `[DataRow]` | `[Arguments]` | |
| `[DynamicData]` | `[TestData]`, `[ClassDataSource<T>]` | |
| `[DataSource]` (CSV, database) | `[TestData]` over a member you write | |
| `TestContext` property | `ITestContext` constructor injection, `TestContext.Current` | |
| `TestContext.WriteLine` | `ITestContext.Output.WriteLine`, injected `ITestOutput` | |
| `TestContext.AddResultFile` | `ITestContext.AttachArtifact` | |
| `[DeploymentItem]` | none | See [Deliberate non-equivalents](#deliberate-non-equivalents) |
| `[TestCategory]` | `[Category]` | |
| `[TestProperty]`, `[Owner]` | `[Tag]` | `[Tag]` carries a name, not a name/value pair |
| `[Priority]` | `[Tag]` for filtering, `[ExecutionPriority]` for order | MSTest's `[Priority]` is metadata, not ordering |
| `[Timeout]` | `[Timeout]` | |
| `[Ignore]` | `[Skip]` | |
| `Assert.Inconclusive` | `Assert.Skip` | Reported as skipped; there is no inconclusive outcome |
| `[ExpectedException]` | `Assert.Throws<T>` | |
| `[Retry]` | `[Retry]`, `[Retry<TPolicy>]` | |
| `[DoNotParallelize]` | `[NotInParallel]` | |
| `[Parallelize(Workers = n)]` | `[ParallelLimit(n)]` on each class | No suite-wide equivalent; see [Parallelism and ordering](#parallelism-and-ordering) |
| `[DescriptionAttribute]` | `[DisplayName]` | Changes the reported name |
| `Assert.AreEqual` | `Assert.Equal` | Same argument order |
| `StringAssert.*` | `Assert.StartsWith`, `EndsWith`, `Contains` | |
| `CollectionAssert.*` | `Assert.Contains`, `Equivalent`, `Subset`, `Empty` | |

## Project setup

Remove the MSTest packages and add the single NextUnit package.

```bash
dotnet remove package MSTest
dotnet remove package MSTest.TestAdapter
dotnet remove package MSTest.TestFramework
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
    <PackageReference Include="MSTest" Version="3.7.0" />
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
`Program.cs`, or a separate analyzer reference. A `.runsettings` file that only configured the MSTest
adapter can go; parallelism and filtering move to attributes and command-line options.

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

MSTest:

```csharp mstest
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class CalculatorTests
{
    [TestMethod]
    public void Add_ReturnsSum()
    {
        Assert.AreEqual(4, 2 + 2);
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

`[TestClass]` disappears, because a public class holding `[Test]` methods is already discovered.
`Assert.AreEqual` becomes `Assert.Equal` with the same expected-then-actual argument order, so that
rewrite is a rename.

There is no `using` for NextUnit in `ImplicitUsings`, so add `using NextUnit;` to every test file, or
a `global using NextUnit;` once per project.

## Lifecycle

MSTest uses six attributes with signature rules attached. NextUnit uses two attributes and a scope,
with no required parameters.

MSTest:

```csharp mstest
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class OrderTests
{
    [ClassInitialize]
    public static void CreateSchema(TestContext context) { }

    [TestInitialize]
    public void ResetRows() { }

    [TestCleanup]
    public void ClearRows() { }

    [ClassCleanup]
    public static void DropSchema() { }

    [TestMethod]
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

`LifecycleScope` has four values you will use: `Test`, `Class`, `Assembly`, and `Session`.
`[AssemblyInitialize]` and `[AssemblyCleanup]` become assembly-scoped hooks, and session scope is the
outermost, wrapping the whole run under Microsoft.Testing.Platform.

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

Hooks may return `void`, `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>`, so the async initializers
MSTest supports convert directly. A class-scoped hook takes no `TestContext` parameter; inject
`ITestContext` into the test class when a test needs the context.

## Data sources

### Inline rows

`[DataRow]` becomes `[Arguments]`, one attribute per row.

MSTest:

```csharp mstest
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class AdditionTests
{
    [TestMethod]
    [DataRow(1, 2, 3)]
    [DataRow(5, 5, 10)]
    public void Add(int a, int b, int expected)
    {
        Assert.AreEqual(expected, a + b);
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

### Member sources

`[DynamicData]` becomes `[TestData]`. There is no `DynamicDataSourceType` argument, because the
generator resolves a field, a property, or a method by name at build time.

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
typeof(SharedCases))]`. A source member that is missing or misnamed is reported as `NU0003` at build
time rather than at run time.

### Per-row metadata

MSTest names a row through `[DataRow(..., DisplayName = "...")]`, which covers naming but not
filtering or skipping. `TestDataRow<T>` covers all three, and it is typed, so the compiler checks the
row shape against the method signature. A tuple is spread across the parameters without changing the
method.

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

### Class sources

A data source that is a type rather than a member becomes `[ClassDataSource<T>]`, and `Shared`
controls how many instances exist.

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
    [ClassDataSource<MultiplicationData>(Shared = SharedType.PerAssembly)]
    public void MultipliesAgain(int a, int b, int expected) => Assert.Equal(expected, a * b);
}
```

`SharedType` offers `None`, `PerClass`, `PerAssembly`, `PerSession`, and `Keyed` with a `Key`, which
covers the sharing patterns MSTest expresses through static fields and `[ClassInitialize]`.

### Combinatorial parameters

MSTest has no combinatorial data source, so a full matrix is usually written out as `[DataRow]` lines.
NextUnit generates the product from per-parameter attributes.

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

### Asynchronous and deferred sources

A `[TestData]` member may return `IAsyncEnumerable<T>`, `Task<TCollection>`, or
`ValueTask<TCollection>`, which is how a source that reads a file or calls a service is written
without blocking. Rows are still enumerated once during discovery, so each row stays an individually
selectable test case.

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
unless startup cost is the problem you are solving. This is the closest replacement for MSTest's
`[DataSource]` reading an external CSV or database table.

## Assertions

MSTest splits assertions across `Assert`, `StringAssert`, and `CollectionAssert`. NextUnit has one
`Assert` class, and the expected value always comes first.

| MSTest | NextUnit |
| ------ | -------- |
| `Assert.AreEqual(expected, actual)` | `Assert.Equal(expected, actual)` |
| `Assert.AreNotEqual(other, actual)` | `Assert.NotEqual(other, actual)` |
| `Assert.AreEqual(expected, actual, delta)` | `Assert.Equal(expected, actual, tolerance: delta)` |
| `Assert.IsTrue(value)` | `Assert.True(value)` |
| `Assert.IsFalse(value)` | `Assert.False(value)` |
| `Assert.IsNull(value)` | `Assert.Null(value)` |
| `Assert.IsNotNull(value)` | `Assert.NotNull(value)` |
| `Assert.AreSame(expected, actual)` | `Assert.Same(expected, actual)` |
| `Assert.AreNotSame(other, actual)` | `Assert.NotSame(other, actual)` |
| `Assert.ThrowsException<T>(() => ...)` | `Assert.Throws<T>(() => ...)` |
| `Assert.ThrowsExceptionAsync<T>(...)` | `await Assert.ThrowsAsync<T>(...)` |
| `Assert.Fail(message)` | `Assert.Fail(message)` |
| `Assert.Inconclusive(reason)` | `Assert.Skip(reason)` |
| `StringAssert.StartsWith(text, prefix)` | `Assert.StartsWith(prefix, text)` |
| `StringAssert.EndsWith(text, suffix)` | `Assert.EndsWith(suffix, text)` |
| `StringAssert.Contains(text, part)` | `Assert.Contains(part, text)` |
| `CollectionAssert.Contains(items, item)` | `Assert.Contains(item, items)` |
| `CollectionAssert.DoesNotContain(items, item)` | `Assert.DoesNotContain(item, items)` |
| `CollectionAssert.AreEquivalent(expected, actual)` | `Assert.Equivalent(expected, actual)` |
| `CollectionAssert.IsSubsetOf(subset, superset)` | `Assert.Subset(subset, superset)` |

Note the argument order in the string and collection rows: MSTest passes the value under test first,
and NextUnit passes the expected value first. That reversal still compiles, so it is the mistake to
watch for during a bulk rewrite.

`[ExpectedException]` has no equivalent, because it passes whenever the exception escapes anywhere in
the method. Assert on the call that should throw, and use the returned exception for further checks.
The async form returns a `Task<TException>` rather than the exception itself, so it has to be
awaited: an un-awaited call never observes the failure, and the test passes whatever the delegate
does.

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

`[TestCategory]` becomes `[Category]`, and `[Tag]` adds a second axis for the metadata MSTest
expresses with `[TestProperty]`, `[Owner]`, or `[Priority]`.

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

MSTest's `--filter "TestCategory=Integration&Priority=1"` expression language has no equivalent.
NextUnit exposes one option per axis, and each option may be repeated:

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
`Assert.SkipUnless` cover the runtime form that `Assert.Inconclusive` is often used for, and
`Assert.SkipOnWindows`, `SkipOnLinux`, `SkipOnMacOS`, and `SkipOnFreeBSD` skip by operating system.
`[Explicit]` marks a test that runs only when asked for, with `--explicit`.

## Parallelism and ordering

MSTest parallelizes only when the assembly opts in with `[Parallelize]`. NextUnit runs tests in
parallel unless you opt out, so check your classes for shared state before the first run.

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

`[NotInParallel]` is the equivalent of `[DoNotParallelize]`, and it also accepts constraint keys, so
classes that share one resource can exclude each other without serializing the whole run.

There is no suite-wide parallelism setting. `ParallelLimitAttribute` accepts an assembly target, but
the generator reads `[ParallelLimit]` only from a test method and its containing class, so an
assembly-level declaration compiles and is then ignored. An assembly-wide
`[Parallelize(Workers = 4)]` therefore becomes `[ParallelLimit(4)]` on each class that needs it; a
class without one is bounded by the processor count. `[Timeout]` is the attribute that does resolve
from the assembly as well as the class and method.

MSTest has no built-in ordering. NextUnit expresses order as a dependency or a priority:

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

Higher `[ExecutionPriority]` values run first. Priority only orders tests at the same dependency
level; `[DependsOn]` is the hard constraint, and it takes `ProceedOnFailure` when a dependent should
still run after its prerequisite fails. `[ExecutionPriority]` is ordering, not the reporting metadata
MSTest's `[Priority]` provides -- use `[Tag]` if you were filtering on priority.

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

`[Retry(count)]` counts the first attempt, so `[Retry(3)]` means at most three runs, and the optional
second argument is a delay in milliseconds between attempts. A `[Timeout]` budget applies to each
attempt separately, and timeouts, runtime skips, and cancellation are never retried.

To decide per failure rather than retrying everything, implement `IRetryPolicy` and attach it with
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
and not nested in a private scope. `ITestContext.RetryAttempt` reports the attempt currently running,
starting at 1.

MSTest has no culture attributes, so tests that format or parse dates, numbers, or currency usually
set the culture by hand in `[TestInitialize]`. NextUnit declares it:

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

`[Culture]`, `[UICulture]`, and `[InvariantCulture]` apply at assembly, class, and method level, each
axis resolves on its own, and the most specific declaration wins. The culture covers the constructor,
the test-scoped hooks, the test method, and disposal, is restored afterwards, and is isolated from
tests running in parallel.

## Test context, output, and attachments

MSTest sets a public `TestContext` property on the test class. NextUnit injects `ITestContext` or
`ITestOutput` into the constructor, and `TestContext.Current` is available where injection is
inconvenient.

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

`TestContext.AddResultFile` becomes `ITestContext.AttachArtifact`, which takes a path or an `Artifact`
with a description and MIME type.

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
and a `StateBag` for passing values between hooks and the test. There is no equivalent of
`CurrentTestOutcome`, because a hook that needs to know the outcome is usually better written as an
assertion.

## Deliberate non-equivalents

These are absent by design rather than unimplemented. Each entry states what to use instead.

| MSTest feature | Why NextUnit has no equivalent | Use instead |
| -------------- | ------------------------------ | ----------- |
| `[DeploymentItem]` | NextUnit runs tests from the build output rather than copying into a per-run deployment directory, so there is nothing to deploy into | `<None Include="fixture.json" CopyToOutputDirectory="PreserveNewest" />`, resolved from `AppContext.BaseDirectory` |
| `TestContext` as a settable property | A property assigned by the framework cannot be verified at compile time | Constructor injection of `ITestContext`, or `TestContext.Current` |
| `Assert.Inconclusive` | An inconclusive outcome is a third result that every reporter, filter, and exit code has to model | `Assert.Skip`, `Assert.SkipWhen`, `Assert.SkipUnless` |
| `[ExpectedException]` | It passes when the exception escapes anywhere in the method, including from setup | `Assert.Throws<T>` around the call that should throw |
| `Assert.IsInstanceOfType` | Pattern matching already does this, with better failure information from the compiler | `Assert.True(value is T)`, or a cast that documents the intent |
| `[Priority]` as a filter axis | Priority in NextUnit means execution order, and one attribute cannot mean both | `[Tag]` for filtering, `[ExecutionPriority]` for order |
| `[TestProperty]` name/value pairs | Free-form key/value metadata has no filtering story that stays simple | `[Category]` and `[Tag]` |
| `--filter` expression language | One option per axis is enough to script and to explain | `--category`, `--tag`, `--test-name`, `--test-name-regex` |
| `[DataSource]` (CSV, database, XML) | Binding rows to an external store at run time cannot be verified at build time or trimmed for Native AOT | `[TestData]` over a member that reads the source, with `DeferredEnumeration` when it is large |

The [project roadmap](../PLANS.md) records what is planned and what is explicitly not planned. If a
missing piece blocks a real migration, open an issue with the workflow it blocks.

## Migration checklist

1. Replace the MSTest packages with `NextUnit`, and select Microsoft.Testing.Platform in `global.json`
   if you run `dotnet test`.
2. Add `using NextUnit;` to each test file, or one `global using NextUnit;`.
3. Delete `[TestClass]`, and rewrite the six lifecycle attributes as `[Before]` and `[After]` with a
   scope, dropping the `TestContext` parameters.
4. Rewrite `[DataRow]` as `[Arguments]`, and `[DynamicData]` as `[TestData]`.
5. Rename the `Assert` methods, and fold `StringAssert` and `CollectionAssert` into `Assert`, checking
   the expected/actual order.
6. Replace `[ExpectedException]` with `Assert.Throws<T>` around the failing call.
7. Audit for shared state, because tests now run in parallel by default.
8. Replace `--filter` expressions and `.runsettings` parallel settings with attributes, the
   `--category` family of options, or the `NEXTUNIT_*` variables.
9. Run the suite and compare the test count against the MSTest run before deleting anything.

## Further reading

- [Getting Started](GETTING_STARTED.md)
- [Migrating from NUnit](MIGRATION_FROM_NUNIT.md)
- [Migrating from xUnit](MIGRATION_FROM_XUNIT.md)
- [Best Practices](BEST_PRACTICES.md)
- Sample tests covering every feature: [samples/NextUnit.SampleTests](../samples/NextUnit.SampleTests)
