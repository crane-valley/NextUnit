# Changelog

All notable changes to NextUnit will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned for v1.2
- CLI arguments for category/tag filtering (--category, --tag, --exclude-category, --exclude-tag)
- Test output/logging integration
- Session-scoped lifecycle
- Performance benchmarks with large test suites (1,000+ tests)

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
  - AND logic between category and tag filters
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
