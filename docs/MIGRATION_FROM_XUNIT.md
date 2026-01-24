# Migrating from xUnit to NextUnit

This guide helps you migrate your xUnit tests to NextUnit.
The good news: NextUnit is designed to be familiar to xUnit users, so migration is straightforward!

## Why Migrate to NextUnit?

- **Faster test discovery** - Source generators vs runtime reflection
- **Native AOT support** - Full trim compatibility
- **Better parallel control** - Fine-grained `[ParallelLimit]` and `[NotInParallel]`
- **Multi-scope lifecycle** - Test, Class, and Assembly scopes
- **Clearer attribute names** - `[Test]` instead of `[Fact]`, no confusing `[Theory]`
- **Same assertion API** - Your `Assert.*` calls work unchanged

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
# Add NextUnit (includes Core, Generator, TestAdapter, and Microsoft.NET.Test.Sdk)
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
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NextUnit" Version="1.10.0" />
  </ItemGroup>
</Project>
```

**Note**: The `NextUnit` meta-package automatically includes all required dependencies
(NextUnit.Core, NextUnit.Generator, NextUnit.TestAdapter, and Microsoft.NET.Test.Sdk).
No `OutputType=Exe` or `Program.cs` is needed - NextUnit uses a VSTest adapter.

## Step 2: Update Test Attributes

### Basic Tests

**xUnit**:

```csharp
[Fact]
public void Add_TwoNumbers_ReturnsSum()
{
    var result = 2 + 2;
    Assert.Equal(4, result);
}
```

**NextUnit**:

```csharp
[Test]  // Just change [Fact] to [Test]
public void Add_TwoNumbers_ReturnsSum()
{
    var result = 2 + 2;
    Assert.Equal(4, result);  // Assertions unchanged!
}
```

### Parameterized Tests

**xUnit**:

```csharp
[Theory]
[InlineData(1, 2, 3)]
[InlineData(5, 5, 10)]
[InlineData(10, -5, 5)]
public void Add_ParameterizedTests(int a, int b, int expected)
{
    var result = a + b;
    Assert.Equal(expected, result);
}
```

**NextUnit**:

```csharp
[Test]  // Use [Test] instead of [Theory]
[Arguments(1, 2, 3)]        // [Arguments] instead of [InlineData]
[Arguments(5, 5, 10)]
[Arguments(10, -5, 5)]
public void Add_ParameterizedTests(int a, int b, int expected)
{
    var result = a + b;
    Assert.Equal(expected, result);  // Same!
}
```

### Skipping Tests

**xUnit**:

```csharp
[Fact(Skip = "Not implemented yet")]
public void FutureFeature()
{
    // ...
}
```

**NextUnit**:

```csharp
[Test]
[Skip("Not implemented yet")]  // Separate [Skip] attribute
public void FutureFeature()
{
    // ...
}
```

### Test Categorization

**xUnit**:

```csharp
[Fact]
[Trait("Category", "Integration")]
[Trait("Priority", "High")]
public void DatabaseTest() { }
```

**NextUnit**:

```csharp
[Test]
[Category("Integration")]  // Clearer attribute names
[Tag("High")]               // Tags for additional metadata
public void DatabaseTest() { }
```

## Step 3: Convert Fixtures

### Class Fixtures

**xUnit**:

```csharp
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
public class DatabaseTests
{
    private DatabaseConnection? _connection;

    [Before(LifecycleScope.Class)]  // Runs once before all tests
    public void SetupDatabase()
    {
        _connection = new DatabaseConnection();
    }

    [After(LifecycleScope.Class)]  // Runs once after all tests
    public void CleanupDatabase()
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

### Collection Fixtures

**xUnit**:

```csharp
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

Good news: **Most assertions work unchanged!**

### Assertions That Work Exactly the Same

```csharp
// Basic assertions - identical
Assert.True(condition);
Assert.False(condition);
Assert.Equal(expected, actual);
Assert.NotEqual(notExpected, actual);
Assert.Null(value);
Assert.NotNull(value);

// Exception assertions - identical
Assert.Throws<Exception>(() => { });
Assert.ThrowsAsync<Exception>(async () => { });

// Collection assertions - identical
Assert.Contains(item, collection);
Assert.DoesNotContain(item, collection);
Assert.Empty(collection);
Assert.NotEmpty(collection);
Assert.Single(collection);
Assert.All(collection, item => { });

// String assertions - identical
Assert.StartsWith(prefix, text);
Assert.EndsWith(suffix, text);
Assert.Contains(substring, text);

// Numeric assertions - identical
Assert.InRange(value, min, max);
Assert.NotInRange(value, min, max);
```

### Assertions Not Yet Implemented (v1.0)

These will be added in future versions:

```csharp
// Advanced collection assertions (use alternatives)
Assert.Collection(...)  // → Use Assert.All + Assert.Equal
Assert.IsType<T>(...)   // → Use pattern matching: obj is T
Assert.IsAssignableFrom<T>(...)  // → Use pattern matching
```

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

```csharp
// Limit concurrent tests in a class to 4
[ParallelLimit(4)]
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

// Default: All tests run in parallel with no limit
public class NormalTests
{
    [Test]
    public void Test1() { }  // Runs in parallel
    
    [Test]
    public void Test2() { }  // Runs in parallel
}
```

## Step 6: Test Ordering

### xUnit Test Ordering

**xUnit**:

```csharp
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
```

## Common Patterns

### Setup and Teardown

**xUnit**:

```csharp
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

```csharp
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
| Test Ordering | Third-party | `[DependsOn]` (built-in) |
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
- Verify the generator is referenced correctly in `.csproj`
- Check that `Program.cs` calls `builder.AddNextUnit()`

### "Tests run in wrong order"

- Add `[DependsOn(nameof(OtherTest))]` for explicit ordering
- Use `[NotInParallel]` if tests must run serially

### "Shared state issues"

- Use `[NotInParallel]` for tests that modify shared state
- Consider class-scoped or assembly-scoped lifecycle for expensive setup

## Getting Help

If you encounter issues during migration:

1. Check the [API Reference](API_REFERENCE.md)
2. Review [Sample Tests](../samples/NextUnit.SampleTests)
3. Open an issue: <https://github.com/crane-valley/NextUnit/issues>

Welcome to NextUnit!
