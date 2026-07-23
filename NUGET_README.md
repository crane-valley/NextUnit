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
- NextUnit.Platform - Microsoft.Testing.Platform integration
- NextUnit.Generator - Source generator for zero-reflection discovery
- NextUnit.Analyzers - Compile-time validation
- Microsoft.Testing.Platform.MSBuild - `dotnet test`, `dotnet run`, and IDE entry point generation
- Microsoft.Testing.Extensions.TrxReport - CI-friendly TRX result files

### Configure Your Test Project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NextUnit" Version="1.15.1" />
  </ItemGroup>
</Project>
```

**Note**: No `OutputType=Exe`, `Program.cs`, or separate analyzer reference is needed. The package
configures Microsoft.Testing.Platform and registers NextUnit automatically.

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
dotnet run --project MyProject.Tests
```

To run an entire repository with `dotnet test` on the .NET 10 SDK, select
Microsoft.Testing.Platform in the repository's `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

## Key Features

- **Zero-reflection execution** - Fast test discovery via source generators
- **Rich assertions** - Collections, strings, numerics with great error messages
- **Test artifacts** - Attach screenshots, logs, videos to test results
- **Combined data sources** - `[Values]`, `[ValuesFromMember]`, `[ValuesFrom<T>]` with Cartesian product
- **Matrix data source** - `[Matrix]` for Cartesian product test generation
- **Class data source** - `[ClassDataSource<T>]` with shared instance support
- **Typed data rows** - Per-row display names, categories, tags, and skip reasons
- **Explicit tests** - `[Explicit]` to exclude from default runs
- **Roslyn analyzers** - Compile-time validation of test code
- **Multi-scope lifecycle** - Test, Class, Assembly scopes
- **Parallel control** - `[ParallelLimit]`, `[NotInParallel("key")]`, `[ParallelGroup("name")]`
- **Execution priority** - `[ExecutionPriority(N)]` for controlling test order
- **Test dependencies** - `[DependsOn]` with `ProceedOnFailure` option
- **Native AOT compatible** - Full trim and AOT support

## Packages

| Package | Description |
| ------- | ----------- |
| **NextUnit** | Single-package setup with runtime, generator, analyzers, and platform integration |
| **NextUnit.Core** | Core attributes, assertions, and execution engine |
| **NextUnit.Platform** | Microsoft.Testing.Platform integration |
| **NextUnit.Generator** | Source generator for test discovery |
| **NextUnit.TestAdapter** | Optional legacy VSTest adapter |

## Documentation

- **[Getting Started Guide](https://github.com/crane-valley/NextUnit/blob/main/docs/GETTING_STARTED.md)**
- **[Migration from xUnit](https://github.com/crane-valley/NextUnit/blob/main/docs/MIGRATION_FROM_XUNIT.md)**
- **[Best Practices](https://github.com/crane-valley/NextUnit/blob/main/docs/BEST_PRACTICES.md)**
- **[Performance methodology and results](https://github.com/crane-valley/NextUnit/blob/main/docs/PERFORMANCE.md)**
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
