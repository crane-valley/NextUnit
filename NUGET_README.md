# NextUnit - Modern Testing for .NET 10+

[![NuGet](https://img.shields.io/nuget/v/NextUnit.svg)](https://www.nuget.org/packages/NextUnit/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/kiyoaki/NextUnit/blob/main/LICENSE)

**NextUnit** is a modern, high-performance test framework for .NET 10+ that combines:
- **TUnit's modern architecture** - Microsoft.Testing.Platform, Native AOT, source generators
- **xUnit's familiar assertions** - Classic `Assert.Equal(expected, actual)`, no fluent syntax

## 🚀 Quick Start

### Installation

**Simple - One Package (Recommended)**:
```bash
dotnet add package NextUnit
```

This meta-package includes everything you need:
- NextUnit.Core - Attributes, assertions, execution engine
- NextUnit.Generator - Source generator for zero-reflection discovery
- NextUnit.Platform - Microsoft.Testing.Platform integration
- Microsoft.Testing.Platform - Required platform dependency

**Advanced - Individual Packages**:
```bash
dotnet add package NextUnit.Core
dotnet add package NextUnit.Generator
dotnet add package NextUnit.Platform
dotnet add package Microsoft.Testing.Platform
```

### Configure Your Test Project

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <EnableMSTestRunner>true</EnableMSTestRunner>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NextUnit" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### Create Program.cs

```csharp
using Microsoft.Testing.Platform.Builder;
using NextUnit.Platform;

var builder = await TestApplication.CreateBuilderAsync(args);
builder.AddNextUnit();
using var app = await builder.BuildAsync();
return await app.RunAsync();
```

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
dotnet run
```

## ✨ Key Features

- **Zero-reflection execution** - 50x faster test discovery via source generators
- **Rich assertions** - Collections, strings, numerics with great error messages
- **Multi-scope lifecycle** - Test, Class, Assembly scopes
- **Parallel control** - `[ParallelLimit]`, `[NotInParallel]`
- **Test dependencies** - `[DependsOn]` for ordered execution
- **Native AOT compatible** - Full trim and AOT support

## 📦 Packages

| Package | Description | Size |
|---------|-------------|------|
| **NextUnit** | Meta-package with all components (recommended) | 4.2 KB |
| **NextUnit.Core** | Core attributes, assertions, execution engine | 32.1 KB |
| **NextUnit.Generator** | Source generator for test discovery | 20.7 KB |
| **NextUnit.Platform** | Microsoft.Testing.Platform integration | 15.4 KB |

**Total Size**: 72.4 KB (ultra-lightweight!)

## 📚 Documentation

- **[Getting Started Guide](https://github.com/kiyoaki/NextUnit/blob/main/docs/GETTING_STARTED.md)**
- **[Migration from xUnit](https://github.com/kiyoaki/NextUnit/blob/main/docs/MIGRATION_FROM_XUNIT.md)**
- **[Best Practices](https://github.com/kiyoaki/NextUnit/blob/main/docs/BEST_PRACTICES.md)**
- **[Full Documentation](https://github.com/kiyoaki/NextUnit)**

## 🔥 What's Different from xUnit?

| Feature | xUnit | NextUnit |
|---------|-------|----------|
| Test Attribute | `[Fact]` | `[Test]` (clearer) |
| Parameterized | `[Theory]` + `[InlineData]` | `[Test]` + `[Arguments]` |
| Discovery | Runtime reflection | Source generator (50x faster) |
| Parallelism | Limited control | Fine-grained with `[ParallelLimit]` |
| Lifecycle | Constructor + `IDisposable` | Multi-scope `[Before]`/`[After]` |
| AOT Support | Limited | Full Native AOT compatible |

## 🎯 Performance

- **Test Discovery**: ~2ms for 86 tests (50x faster than xUnit)
- **Execution**: ~614ms for 86 tests with parallel execution
- **Framework Memory**: ~5MB baseline
- **Zero reflection** in execution path

## 🤝 Contributing

Contributions welcome! See our [Contributing Guide](https://github.com/kiyoaki/NextUnit#contributing).

## 📄 License

[MIT License](https://github.com/kiyoaki/NextUnit/blob/main/LICENSE)

## 🙏 Acknowledgments

Inspired by:
- **TUnit** - Modern architecture, source generators
- **xUnit** - Ergonomic assertions, proven patterns
- **NUnit/MSTest** - Battle-tested reliability

---

**Built with ❤️ for .NET 10+ developers who want modern performance with familiar syntax**
