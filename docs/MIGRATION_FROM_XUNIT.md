# Migrating from xUnit to NextUnit

This guide helps you migrate your xUnit tests to NextUnit.
The good news: NextUnit is designed to be familiar to xUnit users, so migration is straightforward!

## Why Migrate to NextUnit?

- **Faster test discovery** - Source generators vs runtime reflection
- **Native AOT support** - Full trim compatibility
- **Better parallel control** - Fine-grained `[ParallelLimit]` and `[NotInParallel]`
- **Multi-scope lifecycle** - Test, Class, and Assembly scopes
- **Clearer attribute names** - `[Test]` instead of `[Fact]`, no confusing `[Theory]`
- **Familiar assertion API** - Most `Assert.*` calls keep their shape; see [Step 4](#step-4-update-assertions)

## Quick Migration Checklist

- [ ] Update project references
- [ ] Replace `[Fact]` with `[Test]`
- [ ] Replace `[Theory]` + `[InlineData]` with `[Test]` + `[Arguments]`
- [ ] Convert fixtures to lifecycle attributes
- [ ] Update parallel execution configuration
- [ ] Run tests and verify

## Step 1: Update Project References

### Remove xUnit Packages

```bash
dotnet remove package xunit
dotnet remove package xunit.runner.visualstudio
dotnet remove package Microsoft.NET.Test.Sdk
```

### Add NextUnit Packages

```bash
# Add the complete NextUnit package
dotnet add package NextUnit
```

### Update .csproj

**Before (xUnit)**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="xunit" Version="2.6.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.6.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
  </ItemGroup>
</Project>
```

**After (NextUnit)**:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NextUnit" Version="1.19.1" />
  </ItemGroup>
</Project>
```

**Note**: The `NextUnit` meta-package automatically includes all required dependencies
(runtime, Microsoft.Testing.Platform integration, source generator, analyzers, and TRX reporting).
No `OutputType=Exe`, `Program.cs`, or separate analyzer reference is needed.

For repository-wide `dotnet test` on the .NET 10 SDK, select Microsoft.Testing.Platform in
`global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

## Step 2: Update Test Attributes

### Basic Tests

**xUnit**:

```csharp xunit
using Xunit;

public class CalculatorTests
{
    [Fact]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = 2 + 2;
        Assert.Equal(4, result);
    }
}
```

**NextUnit**:

```csharp
using NextUnit;

public class CalculatorTests
{
    [Test]  // Just change [Fact] to [Test]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = 2 + 2;
        Assert.Equal(4, result);  // Assertions unchanged!
    }
}
```

There is no `using` for NextUnit in `ImplicitUsings`, so add `using NextUnit;` to every test file, or a
`global using NextUnit;` once per project.

### Parameterized Tests

**xUnit**:

```csharp xunit
using Xunit;

public class AdditionTests
{
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(5, 5, 10)]
    [InlineData(10, -5, 5)]
    public void Add_ParameterizedTests(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);
    }
}
```

**NextUnit**:

```csharp
using NextUnit;

public class AdditionTests
{
    [Test]  // Use [Test] instead of [Theory]
    [Arguments(1, 2, 3)]        // [Arguments] instead of [InlineData]
    [Arguments(5, 5, 10)]
    [Arguments(10, -5, 5)]
    public void Add_ParameterizedTests(int a, int b, int expected)
    {
        var result = a + b;
        Assert.Equal(expected, result);  // Same!
    }
}
```

`[Test]` is required alongside `[Arguments]`. xUnit infers the test from `[Theory]` plus its data
attribute; NextUnit does not, and reports a data-source attribute without `[Test]` as `NU0013` at
build time.

### Skipping Tests

**xUnit**:

```csharp xunit
using Xunit;

public class FeatureTests
{
    [Fact(Skip = "Not implemented yet")]
    public void FutureFeature()
    {
        // ...
    }
}
```

**NextUnit**:

```csharp
using NextUnit;

public class FeatureTests
{
    [Test]
    [Skip("Not implemented yet")]  // Separate [Skip] attribute
    public void FutureFeature()
    {
        // ...
    }
}
```

### Test Categorization

**xUnit**:

```csharp xunit
using Xunit;

public class TraitTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public void DatabaseTest() { }
}
```

**NextUnit**:

```csharp
using NextUnit;

public class TraitTests
{
    [Test]
    [Category("Integration")]  // Clearer attribute names
    [Tag("High")]              // Tags for additional metadata
    public void DatabaseTest() { }
}
```

## Step 3: Convert Fixtures

### Class Fixtures

**xUnit**:

```csharp xunit
using Xunit;

public class DatabaseFixture : IDisposable
{
    public DatabaseConnection Connection { get; }

    public DatabaseFixture()
    {
        Connection = new DatabaseConnection();
    }

    public void Dispose()
    {
        Connection?.Dispose();
    }
}

public class DatabaseTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public DatabaseTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test1()
    {
        var result = _fixture.Connection.Query("SELECT 1");
        Assert.Equal(1, result);
    }
}
```

**NextUnit**:

```csharp
using NextUnit;

public sealed class DatabaseConnection : IDisposable
{
    public int Query(string sql) => 1;

    public void Dispose() { }
}

public class DatabaseTests
{
    private static DatabaseConnection? _connection;

    [Before(LifecycleScope.Class)]  // Runs once before all tests
    public static void SetupDatabase()
    {
        _connection = new DatabaseConnection();
    }

    [After(LifecycleScope.Class)]  // Runs once after all tests
    public static void CleanupDatabase()
    {
        _connection?.Dispose();
    }

    [Test]
    public void Test1()
    {
        var result = _connection!.Query("SELECT 1");
        Assert.Equal(1, result);
    }
}
```

The class-scoped hooks and the field they share are `static`, and that is not a style choice. NextUnit
builds one instance of the class to run the class-scoped hooks on and a separate instance for each
test, so an instance `[Before(LifecycleScope.Class)]` compiles and runs but writes its fields on an
object no test ever sees. An xUnit `IClassFixture<T>` held in an instance field therefore needs its
state moved as well as its attribute changed: make the field `static`, as above.

Making it `static` shares it more widely than xUnit did, so check the fixture is safe to use
concurrently before you rely on the conversion. xUnit runs the tests within one class one at a time,
which is why a class fixture can hold a connection that is not thread-safe; NextUnit runs them in
parallel unless told otherwise, so the same connection is now reachable from several tests at once.
Add `[NotInParallel]` to the class when the shared object cannot take that.

### Collection Fixtures

**xUnit**:

```csharp xunit
using Xunit;

[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}

[Collection("Database collection")]
public class FirstDatabaseTests
{
    [Fact]
    public void Test1() { }
}

[Collection("Database collection")]
public class SecondDatabaseTests
{
    [Fact]
    public void Test1() { }
}
```

**NextUnit**:

```csharp
using NextUnit;

// Use Assembly-scoped lifecycle for shared setup across classes
public class FirstDatabaseTests
{
    [Before(LifecycleScope.Assembly)]  // Runs once for the assembly
    public static void SetupDatabase()
    {
        // Initialize shared database
    }

    [After(LifecycleScope.Assembly)]
    public static void CleanupDatabase()
    {
        // Clean up shared database
    }

    [Test]
    public void Test1() { }
}

public class SecondDatabaseTests
{
    [Test]
    public void Test1() { }
}
```

## Step 4: Update Assertions

Good news: **most assertions carry over with their call shape unchanged.** That is a narrower promise
than "identical", and the difference matters during a bulk rewrite: a call that still compiles can
still decide differently. The listing below is what the compile check guarantees, and
[Where the behavior differs](#where-the-behavior-differs) is the part to read before you trust a
migrated suite that passes.

### Assertions That Compile Unchanged

Each group below is a signature listing rather than a runnable test: the parameters stand in for the
values you assert on, so the calls read exactly as they do in your xUnit code.

```csharp
using NextUnit;

public static class AssertionsThatCompileUnchanged
{
    // Basic assertions
    public static void Basics(bool condition, int expected, int actual, int notExpected, object? value)
    {
        Assert.True(condition);
        Assert.False(condition);
        Assert.Equal(expected, actual);
        Assert.NotEqual(notExpected, actual);
        Assert.Null(value);
        Assert.NotNull(value);
    }

    // Collection assertions
    public static void Collections(int item, IEnumerable<int> collection)
    {
        Assert.Contains(item, collection);
        Assert.DoesNotContain(item, collection);
        Assert.Empty(collection);
        Assert.NotEmpty(collection);
        Assert.Single(collection);
        Assert.All(collection, element => { });
    }

    // String assertions
    public static void Strings(string prefix, string suffix, string substring, string text)
    {
        Assert.StartsWith(prefix, text);
        Assert.EndsWith(suffix, text);
        Assert.Contains(substring, text);
    }

    // Numeric assertions
    // As in xUnit, min and max are both inclusive: InRange passes when value equals
    // either bound, and NotInRange treats those values as inside the range and fails.
    public static void Ranges(int value, int min, int max)
    {
        Assert.InRange(value, min, max);
        Assert.NotInRange(value, min, max);
    }
}
```

### Where the Behavior Differs

These familiar call shapes survive the migration and change their rule, so the compiler cannot warn
you and a green suite is not proof the migration was faithful.

| Assertion | xUnit | NextUnit |
| --------- | ----- | -------- |
| `Assert.Throws<T>`, `Assert.ThrowsAsync<T>` | Exact exception type; `ThrowsAny<T>` is the assignable form | Matches `T` and its subtypes |
| `Assert.StartsWith`, `EndsWith`, `Contains` (string) | `StringComparison.CurrentCulture` by default | Always `StringComparison.Ordinal` |
| `Assert.All` | Runs every element and aggregates the failures | Stops at the first failing element |
| `Assert.NotEqual` on a collection | Structural, so two arrays holding the same values are equal and the assertion fails | `EqualityComparer<T>.Default`, which is reference equality for arrays and `List<T>`, so the assertion passes |
| `Assert.Equal` on a nested collection | Recurses into the inner collections | Compares the inner collections by reference, so the assertion fails |

The exception rule is the one that turns a red test green: `Assert.Throws<ArgumentException>` accepts
an `ArgumentNullException` here, where xUnit would have rejected it. Where the exact type is the point
of the test, assert it on the returned exception.

The string rule moves in the other direction and only bites on culture-sensitive text, where an
ordinal comparison is stricter than a culture-aware one. Where the culture-aware behavior was the
point, assert the comparison yourself with
`Assert.True(text.StartsWith(prefix, StringComparison.CurrentCulture))`.

The collection rules are the ones worth grepping for, because `Assert.Equal` and `Assert.NotEqual`
disagree with each other here. `Assert.Equal` compares a collection element by element and in order,
which is what xUnit does, so a flat array or `List<T>` carries over. `Assert.NotEqual` does not: it
compares with `EqualityComparer<T>.Default`, and arrays and `List<T>` do not override equality, so
two collections holding the same values are "not equal" to it. `Assert.NotEqual(new[] { 1, 2 }, new[]
{ 1, 2 })` fails in xUnit and passes here, which turns a red test green without a word from the
compiler. Assert the comparison yourself when that is the point of the test:

```csharp
using NextUnit;

public class BasketTests
{
    [Test]
    public void DiscountChangesTheLines()
    {
        int[] before = [1, 2];
        int[] after = [1, 3];

        // Assert.NotEqual would pass on any two distinct arrays, equal contents or not
        Assert.False(before.SequenceEqual(after), "The lines should have changed.");
    }
}
```

Nesting fails the other way, loudly rather than silently. `Assert.Equal` compares the elements
themselves with `object.Equals`, so an inner collection is compared by reference: two equal
`int[][]` values are reported as different. Compare the inner sequences individually when a test
needs that shape. `Assert.Equivalent` is not the fix for either case -- it compares contents while
ignoring order, so it answers a different question than `Assert.Equal` does.

`Assert.ThrowsAsync` returns a `Task<TException>` rather than the exception itself, so it has to be
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
        Assert.Equal(typeof(FormatException), error.GetType());  // Exact type, as xUnit matches
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

### Assertions Without a NextUnit Equivalent

As of 1.19.0, these xUnit assertions have no NextUnit counterpart. Rewrite them:

| xUnit | Rewrite as |
| ----- | ---------- |
| `Assert.Collection(items, i1, i2)` | Materialize the sequence, `Assert.Equal` on the count, then assert each index |
| `Assert.IsType<T>(obj)` | `Assert.Equal(typeof(T), obj.GetType())` |
| `Assert.IsAssignableFrom<T>(obj)` | `Assert.True(obj is T)` |

They are listed as a table rather than as code because there is nothing to compile: the calls do not
exist on NextUnit's `Assert`, which is the point of the section.

Mind the matching rule when rewriting the type assertions. `Assert.IsType<T>` is an exact-type check,
so `obj is T` is not a replacement for it -- a subtype would satisfy the pattern and fail the original
assertion. `Assert.IsAssignableFrom<T>` is the one that means "assignable", and `obj is T` matches it.

Mind the result as well. Both xUnit calls return the value typed as `T`, so
`var typed = Assert.IsType<Order>(result);` is the common shape, and the replacements above return
nothing. Assert first, then cast, keeping whichever matching rule the original used:

```csharp
using NextUnit;

public class OrderTests
{
    [Test]
    public void ReturnsAnOrder()
    {
        object result = new Order(2);

        // Assert.IsType<Order>: exact type, so compare the type itself
        Assert.Equal(typeof(Order), result.GetType());
        var typed = (Order)result;
        Assert.Equal(2, typed.LineCount);
    }

    [Test]
    public void ReturnsSomethingOrderLike()
    {
        object result = new Order(2);

        // Assert.IsAssignableFrom<Order>: a subtype counts, so pattern match
        Assert.True(result is Order, "Expected an Order.");
        var typed = (Order)result;
        Assert.Equal(2, typed.LineCount);
    }
}

public record Order(int LineCount);
```

`Assert.Collection` also checks more than it looks like it does: it requires the element count to
match the number of inspectors and runs a different inspector per position, so `Assert.All`, which
applies one check to every element, is not equivalent on its own.

## Step 5: Parallel Execution

### xUnit Parallelization

**xUnit** (`xunit.runner.json`):

```json
{
  "parallelizeAssembly": true,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 4
}
```

### NextUnit Parallelization

**NextUnit** (attribute-based):

Declare the suite-wide default once, at the top of any file in the project:
`[assembly: ParallelLimit(4)]`. Every test that declares no nearer limit then runs 4 at a time.
Classes and methods override it:

```csharp
using NextUnit;

// A class or method may declare its own, and the nearest declaration wins -- in
// either direction, so this class runs 2 at a time and one declaring 8 would run 8
[ParallelLimit(2)]
public class ResourceIntensiveTests
{
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }
}

// Run tests serially (one at a time)
[NotInParallel]
public class SerialTests
{
    [Test]
    public void Test1() { }

    [Test]
    public void Test2() { }
}

// Declares nothing, so it inherits the assembly default of 4 -- and would be
// bounded by the processor count instead if no level declared a limit at all
public class NormalTests
{
    [Test]
    public void Test1() { }  // Runs in parallel

    [Test]
    public void Test2() { }  // Runs in parallel
}
```

The two settings are not the same shape. `maxParallelThreads` is a hard ceiling that nothing in the
suite can raise, while `[assembly: ParallelLimit]` is the default each test inherits when neither its
method nor its class declares one -- and a nearer declaration replaces it with a larger value just as
readily as with a smaller one. Migrating a suite that relied on `maxParallelThreads` to protect a
shared resource therefore means checking the class-level and method-level limits too; the assembly
value alone does not cap them.

## Step 6: Test Ordering

### xUnit Test Ordering

**xUnit**:

```csharp xunit
using Xunit;

[Collection("Sequential")]
public class OrderedTests
{
    [Fact, TestPriority(1)]
    public void Test1() { }

    [Fact, TestPriority(2)]
    public void Test2() { }
}
```

### NextUnit Test Ordering

**NextUnit**:

```csharp
using NextUnit;

public class OrderedTests
{
    [Test]
    public void Test1() { }

    [Test]
    [DependsOn(nameof(Test1))]  // Explicit dependency
    public void Test2() { }

    [Test]
    [DependsOn(nameof(Test2))]
    public void Test3() { }
}

// Or use ExecutionPriority for tests without dependencies
public class PriorityOrderedTests
{
    [Test]
    [ExecutionPriority(3)]  // Runs first (higher = first)
    public void Test1() { }

    [Test]
    [ExecutionPriority(2)]  // Runs second
    public void Test2() { }

    [Test]
    [ExecutionPriority(1)]  // Runs third
    public void Test3() { }
}
```

## Common Patterns

### Setup and Teardown

**xUnit**:

```csharp xunit
using Xunit;

public class MyTests : IDisposable
{
    public MyTests()  // Constructor = Setup
    {
        // Setup before each test
    }

    public void Dispose()  // Dispose = Teardown
    {
        // Teardown after each test
    }

    [Fact]
    public void Test1() { }
}
```

**NextUnit**:

```csharp
using NextUnit;

public class MyTests
{
    [Before(LifecycleScope.Test)]
    public void Setup()
    {
        // Setup before each test
    }

    [After(LifecycleScope.Test)]
    public void Teardown()
    {
        // Teardown after each test
    }

    [Test]
    public void Test1() { }
}
```

### Async Lifecycle

**xUnit**:

```csharp xunit
using Xunit;

public class AsyncTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await Task.Delay(100);
    }

    public async Task DisposeAsync()
    {
        await Task.Delay(100);
    }
}
```

**NextUnit**:

```csharp
using NextUnit;

public class AsyncTests
{
    [Before(LifecycleScope.Test)]
    public async Task InitializeAsync()
    {
        await Task.Delay(100);
    }

    [After(LifecycleScope.Test)]
    public async Task CleanupAsync()
    {
        await Task.Delay(100);
    }
}
```

## Feature Comparison

| Feature | xUnit | NextUnit |
| ------- | ----- | -------- |
| Basic Tests | `[Fact]` | `[Test]` |
| Parameterized Tests | `[Theory]` + `[InlineData]` | `[Test]` + `[Arguments]` |
| Data Sources | `[MemberData]`, `[ClassData]` | `[TestData]` |
| Skip Tests | `[Fact(Skip="...")]` | `[Skip("...")]` |
| Test Setup | Constructor | `[Before(LifecycleScope.Test)]` |
| Test Teardown | `IDisposable` | `[After(LifecycleScope.Test)]` |
| Class Setup | `IClassFixture<T>` | `[Before(LifecycleScope.Class)]` |
| Collection Fixture | `ICollectionFixture<T>` | `[Before(LifecycleScope.Assembly)]` |
| Parallelization | JSON config | `[ParallelLimit]`, `[NotInParallel]` |
| Test Ordering | Third-party | `[DependsOn]`, `[ExecutionPriority]` (built-in) |
| Assertions | `Assert.*` | `Assert.*` (same!) |
| Test Discovery | Runtime reflection | Source generator (faster) |
| Native AOT | Limited | Full support |

## Migration Tips

1. **Start small**: Migrate one test class at a time
2. **Run both**: Keep xUnit and NextUnit side-by-side during migration
3. **Test incrementally**: Verify each migrated class works before proceeding
4. **Use lifecycle scopes**: Map xUnit fixtures to appropriate NextUnit scopes
5. **Embrace parallel execution**: Most tests should run in parallel
6. **Add dependencies**: Use `[DependsOn]` for integration test ordering

## Troubleshooting

### "Test not discovered"

- Ensure `[Test]` attribute is applied
- Verify the `NextUnit` package reference is present. It supplies the source generator that builds
  the test registry, and it registers NextUnit with the entry point
  `Microsoft.Testing.Platform.MSBuild` generates, so there is no `Program.cs` to write or check.
- If you kept an entry point of your own from the xUnit project, make sure it calls
  `builder.AddNextUnit()`.

### "Tests run in wrong order"

- Add `[DependsOn(nameof(OtherTest))]` for explicit ordering
- Use `[NotInParallel]` if tests must run serially

### "Shared state issues"

- Use `[NotInParallel]` for tests that modify shared state
- Consider class-scoped or assembly-scoped lifecycle for expensive setup

## Getting Help

If you encounter issues during migration:

1. Check the [Best Practices Guide](BEST_PRACTICES.md)
2. Review [Sample Tests](../samples/NextUnit.SampleTests)
3. Open an issue: <https://github.com/crane-valley/NextUnit/issues>

Welcome to NextUnit!
