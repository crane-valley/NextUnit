# NextUnit - Modern Testing for .NET 10+

[![NuGet](https://img.shields.io/nuget/v/NextUnit.svg)](https://www.nuget.org/packages/NextUnit/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/crane-valley/NextUnit/blob/main/LICENSE)

**NextUnit** is a modern, high-performance test framework for .NET 10+ that combines:

- **TUnit's modern architecture** - Microsoft.Testing.Platform, Native AOT, source generators
- **xUnit's familiar assertions** - Classic `Assert.Equal(expected, actual)`, no fluent syntax

## Quick Start

### Installation

**Simple - One Package (Recommended)**:

```bash
dotnet add package NextUnit
```

This meta-package includes everything you need:

- NextUnit.Core - Attributes, assertions, execution engine
- NextUnit.Generator - Source generator for zero-reflection discovery
- NextUnit.TestAdapter - VSTest adapter for Visual Studio Test Explorer
- Microsoft.NET.Test.Sdk - Test platform SDK

### Configure Your Test Project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NextUnit" Version="1.14.0" />
  </ItemGroup>
</Project>
```

**Note**: No `OutputType=Exe` or `Program.cs` needed - NextUnit works as a class library with VSTest adapter.

### Write Your First Test

```csharp
using NextUnit;

public class CalculatorTests
{
    [Test]
    public void Add_TwoNumbers_ReturnsSum()
    {
        var result = 2 + 2;
        Assert.Equal(4, result);
    }

    [Test]
    [Arguments(1, 2, 3)]
    [Arguments(5, 5, 10)]
    public void Add_Parameterized(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }
}
```

### Run Tests

```bash
dotnet test
```

## Key Features

- **Zero-reflection execution** - Fast test discovery via source generators
- **Rich assertions** - Collections, strings, numerics with great error messages
- **Test artifacts** - Attach screenshots, logs, videos to test results
- **Combined data sources** - `[Values]`, `[ValuesFromMember]`, `[ValuesFrom<T>]` with Cartesian product
- **Matrix data source** - `[Matrix]` for Cartesian product test generation
- **Class data source** - `[ClassDataSource<T>]` with shared instance support
- **Explicit tests** - `[Explicit]` to exclude from default runs
- **Roslyn analyzers** - Compile-time validation of test code
- **Multi-scope lifecycle** - Test, Class, Assembly scopes
- **Parallel control** - `[ParallelLimit]`, `[NotInParallel("key")]`, `[ParallelGroup("name")]`
- **Test dependencies** - `[DependsOn]` with `ProceedOnFailure` option
- **Native AOT compatible** - Full trim and AOT support

## Packages

| Package | Description | Size |
| ------- | ----------- | ---- |
| **NextUnit** | Meta-package with all components (recommended) | 4.2 KB |
| **NextUnit.Core** | Core attributes, assertions, execution engine | 32.1 KB |
| **NextUnit.Generator** | Source generator for test discovery | 20.7 KB |
| **NextUnit.TestAdapter** | VSTest adapter for Visual Studio Test Explorer | 15.4 KB |

**Total Size**: 72.4 KB (ultra-lightweight!)

## Documentation

- **[Getting Started Guide](https://github.com/crane-valley/NextUnit/blob/main/docs/GETTING_STARTED.md)**
- **[Migration from xUnit](https://github.com/crane-valley/NextUnit/blob/main/docs/MIGRATION_FROM_XUNIT.md)**
- **[Best Practices](https://github.com/crane-valley/NextUnit/blob/main/docs/BEST_PRACTICES.md)**
- **[Full Documentation](https://github.com/crane-valley/NextUnit)**

## What's Different from xUnit?

| Feature | xUnit | NextUnit |
| ------- | ----- | -------- |
| Test Attribute | `[Fact]` | `[Test]` (clearer) |
| Parameterized | `[Theory]` + `[InlineData]` | `[Test]` + `[Arguments]` |
| Discovery | Runtime reflection | Source generator (faster) |
| Parallelism | Limited control | Fine-grained with `[ParallelLimit]` |
| Lifecycle | Constructor + `IDisposable` | Multi-scope `[Before]`/`[After]` |
| AOT Support | Limited | Full Native AOT compatible |

## Contributing

Contributions welcome! See our [Contributing Guide](https://github.com/crane-valley/NextUnit#contributing).

## License

[MIT License](https://github.com/crane-valley/NextUnit/blob/main/LICENSE)

## Acknowledgments

Inspired by:

- **TUnit** - Modern architecture, source generators
- **xUnit** - Ergonomic assertions, proven patterns
- **NUnit/MSTest** - Battle-tested reliability

---

Built with love for .NET 10+ developers who want modern performance with familiar syntax.
