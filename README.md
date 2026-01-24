# NextUnit

[![.NET](https://github.com/crane-valley/NextUnit/actions/workflows/dotnet.yml/badge.svg)](https://github.com/crane-valley/NextUnit/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/NextUnit.svg)](https://www.nuget.org/packages/NextUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modern, high-performance test framework for .NET 10+ with zero-reflection execution and xUnit-style assertions.

## Features

- **Zero-reflection execution** - Source generators produce delegate-based test registry
- **Familiar assertions** - `Assert.Equal`, `Assert.True`, `Assert.Throws`, etc.
- **Multi-scope lifecycle** - `[Before]`/`[After]` at Test, Class, Assembly, or Session level
- **Fine-grained parallelism** - `[ParallelLimit(N)]`, `[NotInParallel("key")]`, `[ParallelGroup]`
- **Parameterized tests** - `[Arguments]`, `[TestData]`, `[Matrix]` attributes
- **Combined data sources** - `[Values]`, `[ValuesFromMember]`, `[ValuesFrom<T>]` with Cartesian product
- **Class data source** - `[ClassDataSource<T>]` with shared instance support
- **Category/Tag filtering** - `[Category]`, `[Tag]` with CLI and environment variable support
- **Test dependencies** - `[DependsOn]` for ordered execution with `ProceedOnFailure` option
- **Roslyn analyzers** - Compile-time test validation
- **VSTest integration** - Works with Visual Studio Test Explorer and `dotnet test`
- **Native AOT compatible**

## Quick Start

### Installation

```bash
dotnet add package NextUnit
```

### Project Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NextUnit" Version="1.11.0" />
  </ItemGroup>
</Project>
```

### Writing Tests

```csharp
using NextUnit;

public class CalculatorTests
{
    [Test]
    public void Add_ReturnsSum()
    {
        Assert.Equal(4, 2 + 2);
    }

    [Test]
    public void Divide_ThrowsOnZero()
    {
        Assert.Throws<DivideByZeroException>(() => { var x = 1 / 0; });
    }

    [Test]
    [Arguments(2, 3, 5)]
    [Arguments(-1, 1, 0)]
    public void Add_Parameterized(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }
}
```

### Running Tests

```bash
dotnet test                                    # Run all tests
dotnet test --filter "FullyQualifiedName~Calc" # Filter by name
```

## Assertions

| Category | Methods |
| -------- | ------- |
| Basic | `Equal`, `NotEqual`, `True`, `False`, `Null`, `NotNull` |
| Collections | `Contains`, `DoesNotContain`, `Empty`, `NotEmpty`, `Single`, `All` |
| Strings | `StartsWith`, `EndsWith`, `Contains` |
| Numeric | `InRange`, `NotInRange`, `Equal(expected, actual, precision)` |
| Exceptions | `Throws<T>`, `ThrowsAsync<T>` |
| Advanced | `Equivalent`, `Subset`, `Disjoint` |

## Lifecycle Hooks

```csharp
public class DatabaseTests
{
    [Before(LifecycleScope.Test)]   // Before each test
    public void Setup() { }

    [After(LifecycleScope.Test)]    // After each test
    public void Cleanup() { }

    [Before(LifecycleScope.Class)]  // Once before all tests in class
    public void ClassSetup() { }

    [Test]
    public void MyTest() { }
}
```

Scopes: `Test`, `Class`, `Assembly`, `Session`

## Parallel Execution

```csharp
[NotInParallel]      // Run tests serially
public class SlowTests { }

[ParallelLimit(2)]   // Max 2 concurrent tests
public class ModerateTests { }
```

## Filtering

```csharp
[Category("Integration")]
[Tag("Slow")]
public class MyTests { }
```

```bash
# Environment variables
NEXTUNIT_INCLUDE_CATEGORIES=Integration dotnet test
NEXTUNIT_EXCLUDE_TAGS=Slow dotnet test
```

## Performance

| Metric | Result |
| ------ | ------ |
| Discovery (1,000 tests) | ~2ms |
| Per-test overhead | ~0.7ms |
| Throughput | 1,852 tests/sec |

See [benchmark results](tools/speed-comparison/results/BENCHMARK_RESULTS.md) for comparison with xUnit, NUnit, and MSTest.

## Documentation

- [Getting Started](docs/GETTING_STARTED.md)
- [Migration from xUnit](docs/MIGRATION_FROM_XUNIT.md)
- [Best Practices](docs/BEST_PRACTICES.md)
- [Performance Analysis](docs/PERFORMANCE.md)
- [CI/CD Integration](docs/CI_CD_INTEGRATION.md)
- [Changelog](CHANGELOG.md)

### Sample Projects

- [Class Library Testing](samples/ClassLibrary.Sample.Tests/) - Business logic testing patterns
- [Console App Testing](samples/Console.Sample.Tests/) - CLI argument parsing, file processing
- [Framework Tests](samples/NextUnit.SampleTests/) - All NextUnit features demonstrated

## Contributing

1. Open an issue to discuss your idea
2. Fork and create a feature branch
3. Write tests for your changes
4. Submit a PR

**Note**: English-only for code, comments, and documentation.

```bash
dotnet build --configuration Release
dotnet test samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj
```

## License

[MIT License](LICENSE)

## Acknowledgments

Inspired by [TUnit](https://github.com/thomhurst/TUnit) (architecture),
[xUnit](https://github.com/xunit/xunit) (assertions), and NUnit/MSTest.
