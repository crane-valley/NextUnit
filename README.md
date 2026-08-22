# NextUnit

[![.NET](https://github.com/crane-valley/NextUnit/actions/workflows/dotnet.yml/badge.svg)](https://github.com/crane-valley/NextUnit/actions/workflows/dotnet.yml)
[![NuGet](https://img.shields.io/nuget/v/NextUnit.svg)](https://www.nuget.org/packages/NextUnit/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A modern, high-performance test framework for .NET 10+ with zero-reflection execution and xUnit-style assertions.

This README, like the rest of the documentation on `main`, describes NextUnit as it stands there while pinning the
latest released version, so between releases it can mention an API the pinned version does not ship yet; to read it as
it stood for an earlier release, check out that release's git tag.

## Features

- **Zero-reflection execution** - Source generators produce delegate-based test registry
- **Familiar assertions** - `Assert.Equal`, `Assert.True`, `Assert.Throws`, `Assert.Same`, `Assert.DoesNotThrow`, etc.
- **Async tests** - `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>` return types for tests and lifecycle hooks
- **Multi-scope lifecycle** - `[Before]`/`[After]` at Test, Class, Assembly, or Session level
- **Fine-grained parallelism** - `[ParallelLimit(N)]`, `[NotInParallel("key")]`, `[ParallelGroup]`
- **Execution priority** - `[ExecutionPriority(N)]` for controlling test execution order
- **Parameterized tests** - `[Arguments]`, `[TestData]`, `[Matrix]`, and typed per-row metadata
- **Async data sources** - `[TestData]` accepts `IAsyncEnumerable<T>` and task-wrapped collection members
- **Deferred data sources** - opt a very large `[TestData]` source out of discovery-time enumeration
- **Combined data sources** - `[Values]`, `[ValuesFromMember]`, `[ValuesFrom<T>]` with Cartesian product
- **Class data source** - `[ClassDataSource<T>]` with shared instance support
- **Category/Tag filtering** - `[Category]`, `[Tag]` with CLI and environment variable support
- **Selective retry** - `[Retry(N)]`, or `[Retry<TPolicy>(N)]` with an async `IRetryPolicy` that decides
  per exception; the attempt number is on `ITestContext` and the attempt count reaches the failure output
- **Deterministic culture** - `[Culture]`, `[UICulture]`, `[InvariantCulture]` at assembly, class, or
  method level, restored after every test and isolated from tests running in parallel
- **Test dependencies** - `[DependsOn]` for ordered execution with `ProceedOnFailure` option
- **Explicit tests** - `[Explicit]` to exclude from default runs
- **Roslyn analyzers** - Compile-time test validation
- **Microsoft.Testing.Platform integration** - Works with `dotnet run`, `dotnet test`, and IDE test explorers
- **ASP.NET Core integration** - `NextUnit.AspNetCore` package for web API testing
- **Native AOT compatible**

## Quick Start

### New Project

```bash
dotnet new install NextUnit.Templates
dotnet new nextunit -n MyProject.Tests
```

The generated project references only the `NextUnit` package and contains one passing example test.

### Existing Project

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
    <PackageReference Include="NextUnit" Version="4.0.0" />
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
dotnet run --project MyProject.Tests          # Run one test project
dotnet test                                   # Run all tests when MTP is selected in global.json
```

With the .NET 10 SDK, `dotnet test` selects Microsoft.Testing.Platform at repository scope:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

NextUnit repositories can copy the checked-in `global.json`; `dotnet run` needs no repository-level
configuration.

## Assertions

| Category | Methods |
| -------- | ------- |
| Basic | `Equal`, `NotEqual`, `True`, `False`, `Null`, `NotNull` |
| Identity | `Same`, `NotSame`, `Fail` |
| Collections | `Contains`, `DoesNotContain`, `Empty`, `NotEmpty`, `Single`, `All` |
| Strings | `StartsWith`, `EndsWith`, `Contains` |
| Numeric | `InRange`, `NotInRange`, `Equal(expected, actual, precision)`, `Equal(expected, actual, tolerance)`, `NotEqual(expected, actual, tolerance)` |
| Exceptions | `Throws<T>`, `ThrowsAsync<T>`, `DoesNotThrow`, `DoesNotThrowAsync` |
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
NEXTUNIT_INCLUDE_CATEGORIES=Integration dotnet run --project MyProject.Tests
NEXTUNIT_EXCLUDE_TAGS=Slow dotnet run --project MyProject.Tests
```

## Expansion Limits

`[Matrix]`, `[Arguments]`, `[Repeat]`, and parameter-level data sources (`[Values]`,
`[ValuesFromMember]`, `[ValuesFrom]`) multiply, so a small edit can ask for millions of test cases.
NextUnit caps those at **10000** test cases per test method and fails fast instead of expanding. The
generator reports `NEXTUNIT013` at compile time for an expansion it can size from the attributes
alone -- `[Matrix]`, `[Arguments]`, `[Repeat]`, and combinations whose parameters are all `[Values]`.
A combined data source that includes a runtime-resolved member (`[ValuesFromMember]`, `[ValuesFrom]`)
has a size known only once that member runs, so its cap is enforced at discovery, which throws when
the resolved product exceeds the limit. Neither ever truncates -- a shortened run would report green
over tests that never ran.

`[Repeat]` multiplies every data source like any other factor: `[Repeat(5)]` beside `[Values(1, 2)]`,
`[TestData(nameof(Rows))]`, or `[ClassDataSource<Rows>]` runs five test cases per combination or per
row, each carrying the same `#n` id suffix a repeated `[Arguments]` case gets. For these three the
product is formed at discovery rather than at build time, because a parameter source, a member, and
a data source class all have a length that is known only once they run -- or during execution, for a
`[TestData]` source that defers its enumeration.

The cap covers the expansions NextUnit performs itself. It does not limit how many rows a
`[TestData]` or `[ClassDataSource]` member returns, because that member is your code: bounding its
row count would not bound its running time, and a large row set is a supported case. For `[TestData]`,
`DeferredEnumeration` keeps discovery cheap over a large source by reporting one placeholder and
enumerating rows only during execution; `[ClassDataSource]` has no deferred mode and always
materializes its rows at discovery. A `[Repeat]` count beside either is capped even though the rows
are not: the count is written in your attribute rather than returned by your code, so discovery
refuses a count larger than the cap and says so, without ever bounding the rows it multiplies. Within
the cap, a deferred source keeps its single placeholder whatever the count is, and the rows it expands
into during execution carry the repeat suffix like any other row.

Raise the cap for the project, which raises it in both places:

```xml
<PropertyGroup>
  <NextUnitMaxTestCasesPerMethod>50000</NextUnitMaxTestCasesPerMethod>
</PropertyGroup>
```

Override it for one run, without rebuilding:

```bash
NEXTUNIT_MAX_TEST_CASES_PER_METHOD=70000 dotnet run --project MyProject.Tests
```

The two settings have a precedence, and it is `NEXTUNIT_MAX_TEST_CASES_PER_METHOD` over
`NextUnitMaxTestCasesPerMethod` over the 10000 default. The generator writes the project's cap into
the registry it emits, so discovery starts from the number the build enforced instead of from the
default: raising the property alone raises both caps. The environment variable sits above it, and
overrides in both directions -- it can narrow a project that raised its cap as well as widen one that
did not -- because it is the per-run escape hatch, and a run that has to widen the cap should not
need a rebuild to do it.

Leave both unset and the default applies. Set either one to anything that is not a positive 32-bit
integer -- `100O` for `1000`, a `0`, a negative -- and NextUnit refuses it instead of falling back:
the generator reports `NEXTUNIT014` and the build fails, and discovery throws before it resolves a
single data source. A typo in a cap is always looser than the value you typed, so accepting the
default in its place would quietly grant more than you asked for. Each value is still validated
where it is read, so a usable environment variable does not rescue an unusable property.

A test project built by an earlier NextUnit carries no cap in its registry, and discovery reads the
10000 default for it. Rebuild it to have the property propagate.

## Performance

The checked-in comparison suite runs 127 tests with shared bodies through native MTP executables.
A 21-round cyclic comparison balances execution order across five major frameworks and Native AOT
variants of NextUnit and TUnit. The current snapshot is from the PR #160 GitHub Actions run on
Ubuntu 24.04:

| Framework | Version | Median | Median / NextUnit |
| --------- | ------- | -----: | ----------------: |
| NextUnit (AOT) | PR #160 checkout (1.15.1 assembly) | 21.51ms | 0.07x |
| TUnit (AOT) | 1.61.15 | 27.45ms | 0.09x |
| NextUnit | PR #160 checkout (1.15.1 assembly) | 311.43ms | 1.00x |
| MSTest | 4.3.2 | 438.73ms | 1.41x |
| NUnit | 4.6.1 | 512.90ms | 1.65x |
| xUnit | 3.2.2 | 551.40ms | 1.77x |
| TUnit | 1.61.15 | 555.00ms | 1.78x |

The workload is startup-heavy and machine-specific, so these ratios are not universal performance
claims. See the [methodology and limitations](docs/PERFORMANCE.md), [generated results](tools/speed-comparison/results/RUNTIME_COMPARISON.md),
and [raw timings](tools/speed-comparison/results/runtime-comparison.json).

## Documentation

- [Getting Started](docs/GETTING_STARTED.md)
- [Migration from xUnit](docs/MIGRATION_FROM_XUNIT.md)
- [Migration from NUnit](docs/MIGRATION_FROM_NUNIT.md)
- [Migration from MSTest](docs/MIGRATION_FROM_MSTEST.md)
- [ASP.NET Core Testing](docs/ASPNETCORE_TESTING.md)
- [Best Practices](docs/BEST_PRACTICES.md)
- [Performance Analysis](docs/PERFORMANCE.md)
- [CI/CD Integration](docs/CI_CD_INTEGRATION.md)
- [Changelog](CHANGELOG.md)

### Sample Projects

- [Class Library Testing](samples/ClassLibrary.Sample.Tests/) - Business logic testing patterns
- [Console App Testing](samples/Console.Sample.Tests/) - CLI argument parsing, file processing
- [Framework Tests](samples/NextUnit.SampleTests/) - All NextUnit features demonstrated
- [Web API Testing](samples/WebApi.Sample.Tests/) - ASP.NET Core integration testing with `WebApplicationTest<T>`

## Contributing

1. Open an issue to discuss your idea
2. Fork and create a feature branch
3. Write tests for your changes
4. Submit a PR

**Note**: English-only for code, comments, and documentation.

```bash
dotnet build --configuration Release
dotnet test --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj
```

## License

[MIT License](LICENSE)

## Acknowledgments

Inspired by [TUnit](https://github.com/thomhurst/TUnit) (architecture),
[xUnit](https://github.com/xunit/xunit) (assertions), and NUnit/MSTest.
