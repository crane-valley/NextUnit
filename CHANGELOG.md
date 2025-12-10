# Changelog

All notable changes to NextUnit will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.4.0] - 2025-12-09

### Added - Performance Benchmarks and Optimizations

- **Large Test Suite (1,000 tests)** - Created comprehensive benchmark test suite with 1,000 simple tests
  - 20 test classes with 50 tests each
  - Demonstrates excellent scalability and low per-test overhead
  - All tests complete in ~540ms (1,852 tests/second throughput)
  
- **Performance Documentation** - Comprehensive performance analysis at `docs/PERFORMANCE.md`
  - Detailed benchmark results and methodology
  - Per-test overhead analysis (~0.54ms for simple tests)
  - Comparison with xUnit baseline
  - Memory and CPU profiling data
  
- **BenchmarkDotNet Integration** - Professional benchmarking infrastructure
  - `benchmarks/NextUnit.Benchmarks` project with BenchmarkDotNet
  - Test execution benchmarks for various test suite sizes
  - Memory diagnostics and performance profiling
  
### Performance Metrics

- **Test Execution**: 540ms average for 1,000 tests
- **Per-test Overhead**: ~0.54ms per simple test
- **Throughput**: 1,852 tests/second
- **Startup Overhead**: ~750ms (including test discovery)
- **Discovery Time**: < 10ms (source generator advantage)

### Changed

- Updated sample test suite count to 125 tests (121 passed, 4 skipped)
- All existing tests continue to pass with excellent performance

### Notes

- NextUnit demonstrates production-ready performance for large test suites
- Zero-reflection architecture provides competitive per-test overhead
- Source generator provides 50-100x faster test discovery vs reflection-based approaches

### Removed
- **dotnet test support** - Removed `Microsoft.Testing.Platform.MSBuild` package dependency
  - Removed `docs/DOTNET_TEST_SUPPORT.md` documentation
  - Removed `IsTestProject` condition from `NextUnit.targets`
  - Tests should now be executed using `dotnet run` exclusively
  - Simplified project configuration - no longer need `EnableMSTestRunner` property

### Changed
- **README.md** - Updated to state that tests should be executed using `dotnet run`
- **NUGET_README.md** - Removed `EnableMSTestRunner` from example project configuration
- **NextUnit.targets** - Removed conditional logic, now unconditionally sets `OutputType=Exe` and `GenerateProgramFile=false`

## [1.3.1] - 2025-12-09

### Added - dotnet test Support Documentation
- **`Microsoft.Testing.Platform.MSBuild` package dependency** - Added as a direct package reference to NextUnit meta-package
  - Ensures the package is properly restored for consumers
  - Provides MSBuild integration for Microsoft.Testing.Platform
  - Enables optional `dotnet test` support on .NET 10 SDK with proper configuration
- **dotnet test Support Guide** - Comprehensive documentation at `docs/DOTNET_TEST_SUPPORT.md`
  - Explains `dotnet run` vs `dotnet test` differences
  - Provides configuration steps for .NET 10 SDK `dotnet test` support
  - Troubleshooting guide for common issues
  - Clarifies that `dotnet run` is the recommended approach

### Changed
- **README.md** - Updated to reference dotnet test support guide
- **NextUnit.csproj** - Added Microsoft.Testing.Platform.MSBuild as a package dependency to ensure proper restore
- **NextUnit.targets** - Simplified to only set OutputType=Exe (package dependency handles MSBuild integration)

### Fixed
- **Package restore issue** - Microsoft.Testing.Platform.MSBuild now properly restored as a dependency instead of only being referenced in build targets

### Note
- `dotnet run` remains the recommended way to run NextUnit tests
- `dotnet test` requires additional SDK configuration on .NET 10 and later
- See `docs/DOTNET_TEST_SUPPORT.md` for detailed setup instructions

## [1.3.0] - 2025-12-08

### Added - Test Output/Logging Integration
- **`ITestOutput` interface** - xUnit-style test output capability for writing diagnostic messages during test execution
  - `WriteLine(string message)` - Write a line of text to test output
  - `WriteLine(string format, params object?[] args)` - Write formatted text to test output
  - Constructor injection support (similar to xUnit's `ITestOutputHelper`)
- **`TestOutputCapture`** - Thread-safe implementation that captures output for individual test cases
  - Output is captured per-test and included in test results
  - Thread-safe using lock for concurrent access
- **`NullTestOutput`** - No-op implementation for class-level and assembly-level lifecycle instances
  - Used when test class requires ITestOutput but is instantiated for lifecycle methods
  - Singleton pattern for efficiency
- **Source generator enhancements**:
  - Detect constructor parameters requiring `ITestOutput`
  - Add `RequiresTestOutput` property to `TestCaseDescriptor` and `TestDataDescriptor`
  - Generate code to properly instantiate test classes with ITestOutput parameter
- **`TestExecutionEngine` updates**:
  - Create `TestOutputCapture` instance for each test requiring output
  - Inject ITestOutput into test class constructor
  - Capture output and pass to reporting sink
  - Handle ITestOutput in class-level and assembly-level instances
- **`ITestExecutionSink` updates**:
  - Added optional `output` parameter to `ReportPassedAsync`, `ReportFailedAsync`, and `ReportErrorAsync`
  - Output is included in test results via Microsoft.Testing.Platform messaging
- **Microsoft.Testing.Platform integration**:
  - Test output included in `TestNode` properties via `TestMetadataProperty`
  - Output visible in test reports and IDE test explorers
  - Output captured even when tests fail (helpful for debugging)

### Changed
- Test count increased from 116 to 123 tests
  - Added 7 new tests demonstrating test output functionality (`TestOutputTests`)
  - Tests cover simple output, formatted output, multiline output, parameterized tests, async tests, and failed tests with output
- Framework version bumped to 1.3.0

## [1.2.1] - 2025-12-07

### Fixed - Application Dependencies
- **Critical Fix for `deps.json` resolution**:
  - Enforced `OutputType=Exe` for test projects using the `NextUnit` meta-package
  - Added auto-generation of `Program.Main` entry point for proper MTP initialization
  - Resolved `Assembly not found` errors (e.g., `CsvHelper`, `Newtonsoft.Json`) caused by library execution context
  - Ensure correct `deps.json` is generated and used by the test host

## [1.2.0] - 2025-12-06

### Added - CLI Arguments and Session Lifecycle
- **CLI argument support for test filtering**:
  - `--category <name>` - Include only tests with the specified category (can be specified multiple times)
  - `--exclude-category <name>` - Exclude tests with the specified category (can be specified multiple times)
  - `--tag <name>` - Include only tests with the specified tag (can be specified multiple times)
  - `--exclude-tag <name>` - Exclude tests with the specified tag (can be specified multiple times)
  - CLI arguments take precedence over environment variables for flexibility
- **`NextUnitCommandLineOptionsProvider`** - Command-line options provider for Microsoft.Testing.Platform integration
  - Implements `ICommandLineOptionsProvider` for proper CLI registration
  - Supports ArgumentArity.OneOrMore for multiple filter values
- **Session-scoped lifecycle support**:
  - `[Before(LifecycleScope.Session)]` - Execute setup once before all tests in the test session
  - `[After(LifecycleScope.Session)]` - Execute teardown once after all tests in the test session
  - Session lifecycle methods must be static (no instance required)
  - Session setup runs in `CreateTestSessionAsync`
  - Session teardown runs in `CloseTestSessionAsync`
- **Source generator enhancements**:
  - Extract `BeforeSessionMethods` and `AfterSessionMethods` from test classes
  - Properly handle static lifecycle methods (generate correct delegate code)
  - Added `IsStatic` property to `LifecycleMethodDescriptor`
  - Generate appropriate delegates for static vs instance methods

### Changed
- Test count increased from 113 to 116 tests
  - Added 3 new tests demonstrating session-scoped lifecycle
  - `SessionLifecycleTests` class validates session setup/teardown execution order
- Framework version bumped to 1.2.0
- CLI arguments now preferred over environment variables (backward compatible)

### Fixed
- Generator now correctly handles static lifecycle methods
- Session lifecycle properly executes before first test and after last test

## [1.1.0] - 2025-12-06

### Added - Category and Tag Filtering
- **`[Category]` attribute** - Organize tests into broad categories (e.g., "Integration", "Unit")
  - Can be applied to classes and methods
  - Method attributes are combined with class-level attributes
  - Multiple categories supported via multiple attributes
- **`[Tag]` attribute** - Fine-grained test classification (e.g., "Slow", "RequiresNetwork")
  - Can be applied to classes and methods
  - Method attributes are combined with class-level attributes
  - Multiple tags supported via multiple attributes
- **Test filtering via environment variables**:
  - `NEXTUNIT_INCLUDE_CATEGORIES` - Run only tests with specified categories (comma-separated)
  - `NEXTUNIT_EXCLUDE_CATEGORIES` - Exclude tests with specified categories (comma-separated)
  - `NEXTUNIT_INCLUDE_TAGS` - Run only tests with specified tags (comma-separated)
  - `NEXTUNIT_EXCLUDE_TAGS` - Exclude tests with specified tags (comma-separated)
- **`TestFilterConfiguration` class** - Flexible filtering logic
  - Exclude filters take precedence over include filters
  - OR logic between category and tag filters
  - OR logic within same filter type (e.g., multiple categories)
- **Source generator enhancements**:
  - Extract `[Category]` attributes from both method and class level
  - Extract `[Tag]` attributes from both method and class level
  - Emit categories and tags in generated `TestCaseDescriptor` and `TestDataDescriptor`
  - Added `BuildStringArrayLiteral` helper for code generation

### Changed
- Test count increased from 102 to 113 tests
  - Added 11 new tests demonstrating category/tag filtering functionality
  - `CategoryAndTagTests` class (6 tests)
  - `FilterValidationTests` class (5 tests)

## [1.0.0] - 2025-12-06

### Added - TestData Support
- **`[TestData]` attribute support** - Source generator now processes `[TestData]` attributes for runtime test data expansion
  - Static method data sources via `[TestData(nameof(MethodName))]`
  - Static property data sources via `[TestData(nameof(PropertyName))]`
  - External class data sources via `MemberType` property
  - Multiple `[TestData]` attributes on same method
  - Unique test IDs including source type to prevent collisions
- **`TestDataDescriptor`** - Runtime descriptor for dynamic test data expansion
- **`TestDataExpander`** - Resolves data sources at runtime and expands into test cases
- **Generator diagnostic `NEXTUNIT003`** - Warning when both `[Arguments]` and `[TestData]` are used on same method

### Added - Packages
- **NextUnit** meta-package for simplified installation (`dotnet add package NextUnit`)
  - Includes all required components (Core, Generator, Platform)
  - One-command installation matching xUnit/TUnit experience
  - Only 4.2 KB package size

### Added - Core Framework
- `[Test]` attribute for marking test methods (clear alternative to xUnit's `[Fact]`)
- `[Arguments]` attribute for parameterized tests (replaces xUnit's `[Theory]` + `[InlineData]`)
- `[Skip]` attribute with optional reason parameter
- `[DependsOn]` attribute for explicit test ordering
- `[NotInParallel]` attribute for serial execution
- `[ParallelLimit]` attribute for controlled parallel execution
- Multi-scope lifecycle with `[Before]` and `[After]`:
  - `LifecycleScope.Test` - Before/after each test
  - `LifecycleScope.Class` - Before/after all tests in a class
  - `LifecycleScope.Assembly` - Before/after all tests in an assembly

### Added - Assertions (v0.4-alpha)
- **Basic Assertions**:
  - `Assert.True(condition)` / `Assert.False(condition)`
  - `Assert.Equal(expected, actual)` / `Assert.NotEqual(notExpected, actual)`
  - `Assert.Null(value)` / `Assert.NotNull(value)`
  - `Assert.Throws<T>(action)` / `Assert.ThrowsAsync<T>(asyncAction)`

- **Collection Assertions** (NEW in v0.4):
  - `Assert.Contains<T>(item, collection)` - Verify element exists
  - `Assert.DoesNotContain<T>(item, collection)` - Verify element absent
  - `Assert.All<T>(collection, action)` - All elements satisfy condition
  - `Assert.Single<T>(collection)` - Exactly one element
  - `Assert.Empty(collection)` - Collection is empty
  - `Assert.NotEmpty(collection)` - Collection has elements

- **String Assertions** (NEW in v0.4):
  - `Assert.StartsWith(prefix, text)` - String starts with prefix
  - `Assert.EndsWith(suffix, text)` - String ends with suffix
  - `Assert.Contains(substring, text)` - String contains substring

- **Numeric Assertions** (NEW in v0.4):
  - `Assert.InRange<T>(value, min, max)` - Value in range [min, max]
  - `Assert.NotInRange<T>(value, min, max)` - Value outside range

### Added - Source Generator
- Zero-reflection test discovery via Roslyn source generator
- Compile-time test registry generation
- Delegate-based test method invocation (no `MethodInfo.Invoke`)
- Generator diagnostics:
  - `NEXTUNIT001` - Dependency cycle detection
  - `NEXTUNIT002` - Unresolved dependency warnings
- Parameterized test display names showing argument values
- Support for all method signature variations (sync/async, with/without cancellation token)

### Added - Execution Engine
- Microsoft.Testing.Platform integration
- True parallel test execution with constraint enforcement
- Thread-safe lifecycle management:
  - `ConcurrentDictionary` for class contexts
  - `SemaphoreSlim` for synchronization
- Proper `IDisposable` and `IAsyncDisposable` cleanup
- Dependency graph-based test ordering
- Batched parallel execution respecting `[ParallelLimit]`
- Serial execution for `[NotInParallel]` tests

### Added - Documentation
- **GETTING_STARTED.md** - Complete getting started guide
- **MIGRATION_FROM_XUNIT.md** - Comprehensive xUnit migration guide
- **BEST_PRACTICES.md** - Best practices and patterns
- **README.md** - Project overview and quick start
- **PLANS.md** - Implementation roadmap
- **DEVLOG.md** - Development log

### Performance
- **Test Discovery**: ~2ms for 86 tests (50x faster than xUnit)
- **Execution**: ~640ms for 86 tests (parallel execution)
- **Per-test Overhead**: ~7ms average (includes test logic)
- **Framework Memory**: ~5MB baseline
- **Zero reflection** in test execution path
### Technical Details
- **Target Framework**: .NET 10+
- **Native AOT Compatible**: Full support
- **C# Version**: 12.0+
- **Dependencies**:
  - Microsoft.Testing.Platform
  - Microsoft.CodeAnalysis (build-time only)
- **Test Count**: 86 comprehensive tests (100% pass rate)

## [0.4.0-alpha] - 2025-12-03

### Added
- Rich assertion library (11 new methods)
- Collection assertions: Contains, DoesNotContain, All, Single, Empty, NotEmpty
- String assertions: StartsWith, EndsWith, Contains
- Numeric assertions: InRange, NotInRange
- 19 new comprehensive tests in RichAssertionTests.cs
- GETTING_STARTED.md documentation
- MIGRATION_FROM_XUNIT.md guide
- BEST_PRACTICES.md guide

### Changed
- Updated README.md to v0.4-alpha
- Updated PLANS.md with M4 Phase 1 completion
- Updated DEVLOG.md with session notes

### Performance
- Total tests: 86 (was 67, +19)
- Execution time: ~642ms (was ~620ms, +22ms)
- 100% pass rate maintained

## [0.3.0-alpha] - 2025-12-03

### Added
- True parallel execution with `Parallel.ForEachAsync`
- `[ParallelLimit]` enforcement via MaxDegreeOfParallelism
- `[NotInParallel]` enforcement via serial batches
- Thread-safe class and assembly lifecycle
- `ConcurrentDictionary<Type, ClassExecutionContext>` for class contexts
- `SemaphoreSlim` for assembly and class setup synchronization
- Proper resource cleanup (all semaphores disposed)

### Changed
- Refactored ParallelScheduler to use batched execution
- Updated TestExecutionEngine for parallel execution
- Improved thread safety across all lifecycle scopes

### Performance
- Parallel execution fully functional
- Execution time: ~620ms for 67 tests
- Performance maintained while adding thread safety

## [0.2.0-alpha] - 2025-12-02

### Added
- Multi-scope lifecycle: Test, Class, Assembly scopes
- `[Before(LifecycleScope.Class)]` / `[After(LifecycleScope.Class)]`
- `[Before(LifecycleScope.Assembly)]` / `[After(LifecycleScope.Assembly)]`
- ClassExecutionContext for managing class-level state
- Assembly-scoped setup and teardown
- ClassLifecycleTests.cs (5 tests)
- AssemblyLifecycleTests.cs (2 tests)
- RealWorldScenarioTests.cs (21 practical tests)
- Updated README to v0.2-alpha with all M1.5 and M2 features

### Fixed
- Class-scoped lifecycle now runs exactly once per class
- Assembly-scoped lifecycle runs once for entire assembly
- Proper cleanup of class instances after tests

### Performance
- Execution time: ~620ms for 67 tests
- Zero reflection maintained

## [0.1.5-alpha] - 2025-12-02

### Added
- `[Skip]` attribute with optional reason parameter
- Skip reason reporting to Microsoft.Testing.Platform
- `[Arguments]` attribute for parameterized tests
- Enhanced display names showing argument values
- Support for multiple `[Arguments]` attributes per test
- Type-safe delegate generation for parameterized tests
- 11 parameterized test examples
- 4 display name formatting tests

### Changed
- Generator `GetSkipInfo` method for extracting skip information
- Generator `GetArgumentSets` method for collecting test arguments
- Generator `BuildParameterizedDisplayName` for readable test names
- Updated sample tests to demonstrate new features

### Performance
- Added 15 new tests (total: 67)
- Execution time: ~620ms
- Zero reflection maintained

## [0.1.0-alpha] - 2025-12-02

### Added
- Core attribute definitions: `[Test]`, `[Before]`, `[After]`, `[DependsOn]`, `[NotInParallel]`, `[ParallelLimit]`
- Basic assertion library: True, False, Equal, NotEqual, Null, NotNull, Throws, ThrowsAsync
- Test descriptor model: TestCaseDescriptor, LifecycleInfo, ParallelInfo
- Dependency graph builder with cycle detection
- Source generator emitting complete test registry with delegates
- Generator diagnostics: NEXTUNIT001 (cycles), NEXTUNIT002 (unresolved dependencies)
- Delegate-based test and lifecycle method invocation
- Runtime test registry discovery (minimal reflection, cached)
- Microsoft.Testing.Platform integration
- 52 sample tests demonstrating all features

### Technical
- Zero-reflection test execution achieved
- Source generator produces fully-functional test registry
- Delegate-based invocation for all test and lifecycle methods
- Type lookup only (one-time, cached) for test discovery

### Performance
- Test discovery: ~2ms (with caching)
- Execution time: ~600ms for 52 tests
- Per-test overhead: ~11.5ms average
- Framework memory: ~5MB baseline

## [0.0.1-alpha] - 2025-11-28

### Added
- Initial project structure
- Basic framework design
- Microsoft.Testing.Platform integration setup
- Core attribute stubs

---

## Version History Summary

| Version | Date | Tests | Features | Status |
|---------|------|-------|----------|--------|
| 1.0.0 | 2025-12-06 | 102+ | Complete v1.0 feature set | Released |
| 0.4.0-alpha | 2025-12-03 | 86 | Rich Assertions | Released |
| 0.3.0-alpha | 2025-12-03 | 67 | Parallel Execution | Released |
| 0.2.0-alpha | 2025-12-02 | 67 | Multi-scope Lifecycle | Released |
| 0.1.5-alpha | 2025-12-02 | 67 | Skip & Parameterized Tests | Released |
| 0.1.0-alpha | 2025-12-02 | 52 | Zero-reflection Execution | Released |
| 0.0.1-alpha | 2025-11-28 | 0 | Initial Setup | Released |

## Migration Notes

### From xUnit
- Replace `[Fact]` with `[Test]`
- Replace `[Theory]` + `[InlineData]` with `[Test]` + `[Arguments]`
- Replace `IClassFixture<T>` with `[Before(LifecycleScope.Class)]`
- Replace `ICollectionFixture<T>` with `[Before(LifecycleScope.Assembly)]`
- Assertions remain mostly unchanged (same API)

See [MIGRATION_FROM_XUNIT.md](docs/MIGRATION_FROM_XUNIT.md) for complete guide.

---

**Note**: Alpha versions may have breaking changes. Stable v1.0 will follow Semantic Versioning strictly.
