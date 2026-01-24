# NextUnit Development Roadmap

## Current Version: 1.9.0 (Stable)

NextUnit is a production-ready test framework for .NET 10+ with zero-reflection execution, rich assertions, and VSTest integration.

---

## Completed Features

| Version | Key Features |
| ------- | ------------ |
| 1.9.x | Class data sources: `[ClassDataSource<T>]` with shared instance support |
| 1.8.x | Enhanced parallel control: constraint keys, `[ParallelGroup]`, `ProceedOnFailure` |
| 1.7.x | `[DisplayName]` attribute, `[DisplayNameFormatter<T>]`, custom display name formatting |
| 1.6.x | VSTest adapter, Visual Studio Test Explorer, rich failure messages, advanced CLI filtering, `[Timeout]` attribute |
| 1.5.x | Predicate-based collection assertions, xUnit API compatibility |
| 1.4.x | Performance benchmarks, BenchmarkDotNet integration |
| 1.3.x | Test output capture (ITestOutput) |
| 1.2.x | CLI arguments, session lifecycle |
| 1.1.x | Category and tag filtering |
| 1.0.x | Zero-reflection execution, multi-scope lifecycle, parallel control |

**See**: [CHANGELOG.md](CHANGELOG.md) for complete version history.

---

## Upcoming Features

### Priority 0: Internal Refactoring (v1.7.x)

**Status**: In Progress
**Goal**: Improve code quality, reduce duplication, and enhance maintainability

#### 0.1 Critical: Code Duplication Elimination

- [x] Extract `AssemblyLoader` utility (shared assembly loading with exception handling)
  - Consolidated in: `NextUnit.Core/Internal/AssemblyLoader.cs`
- [x] Extract `ExceptionHelper.IsCriticalException()` (shared exception classification)
  - Consolidated in: `NextUnit.Core/Internal/ExceptionHelper.cs`
- [x] Refactor `ExecuteSingleAsync` in TestExecutionEngine (reduce complexity)
  - Already refactored into: `CheckSkipConditionsAsync`, `ExecuteWithRetryAsync`, `ExecuteSingleAttemptAsync`, `ReportFinalExceptionAsync`

#### 0.2 High: Architecture Improvements

- [x] Split `NextUnitGenerator.cs` into focused classes
  - Models: `TestMethodDescriptor`, `LifecycleMethodDescriptor`, `TestDataSource`
  - Helpers: `AttributeHelper` (attribute extraction)
  - Formatters: `ArgumentFormatter`, `DisplayNameFormatter`
  - Builders: `CodeBuilder` (delegates, literals)
  - Emitters: `TestCaseEmitter` (test case/data descriptor emission)
- [x] Extract `VSTestCaseFactory` (consolidate VSTest case creation)
  - Consolidated from: TestDiscoverer, TestExecutor
- [x] Centralize `TestFilter` logic
  - `TestFilterConfiguration` already centralized in Platform for CLI filtering
  - TestAdapter uses VSTest's built-in trait-based filtering (no duplication)
- [x] Consolidate argument formatting methods in Generator (via `ArgumentFormatter`)

#### 0.3 Medium: Code Quality

- [x] Add Regex caching in `TestFilterConfiguration`
  - Wildcard patterns compiled once on assignment, cached for reuse
- [x] Extract `LifecycleScopeConstants` (replace magic numbers 0,1,2,3)
  - Constants defined in Generator to mirror Core enum values
- [x] Extract `DisposeHelper` for IDisposable/IAsyncDisposable pattern
  - Consolidated 4 duplicated disposal patterns in `TestExecutionEngine`
- [x] Resolve TODO comments and unused fields
  - M4 DI work left as planned future work
  - Invalid regex warning comment updated

---

### Priority 1: Core Enhancements

#### 1.1 Runtime Test Skipping

**Status**: Completed (v1.6.7)

- [x] `Assert.Skip(reason)` - Skip test during execution with reason
- [x] `Assert.SkipWhen(condition, reason)` - Conditional skip
- [x] `Assert.SkipUnless(condition, reason)` - Inverse conditional skip
- [x] Platform-specific skipping helpers
- [x] `TestSkippedException` for runtime skip handling

#### 1.2 Timeout Support

**Status**: Completed (v1.6.8)

- [x] `[Timeout(milliseconds)]` attribute per test
- [x] Class-level timeout defaults (method-level overrides class-level)
- [x] Graceful cancellation with cleanup via CancellationToken
- [x] `TestTimeoutException` for clear timeout reporting

#### 1.3 Test Context Injection

**Status**: Completed (v1.6.9)

- [x] `ITestContext` interface with test name, class, assembly info
- [x] Static `TestContext.Current` for async-local access
- [x] Inject via constructor alongside `ITestOutput`
- [x] Access to test properties, categories, and tags at runtime
- [x] Current test timeout and cancellation token
- [x] `StateBag` for test-scoped data storage

#### 1.4 Retry and Flaky Test Support

**Status**: Completed (v1.6.9)

- [x] `[Retry(count)]` attribute for automatic retry on failure
- [x] `[Retry(count, delayMs)]` with configurable delay
- [ ] Conditional retry via `ShouldRetry()` virtual method
- [ ] Retry statistics in test reports
- [x] `[Flaky]` attribute to mark known flaky tests

#### 1.5 Display Name Customization

**Status**: Completed (v1.7.0)

- [x] `[DisplayName("Custom name")]` attribute
- [x] `[DisplayNameFormatter<T>]` for custom formatting logic
- [x] Support for parameterized test display names with `{0}`, `{1}` placeholders

#### 1.6 Enhanced Parallel Control

**Status**: Completed (v1.8.0)
**Goal**: Fine-grained parallelism control

- [x] `[NotInParallel("constraintKey")]` - Constraint-based resource locking
- [x] Multiple constraint keys: `[NotInParallel("Database", "FileSystem")]`
- [x] `[ParallelGroup("groupName")]` - Exclusive group execution
- [x] `[DependsOn(..., ProceedOnFailure = true)]` - Continue despite failures

### Priority 2: Advanced Data Sources

#### 2.1 Matrix Data Source

**Status**: Completed (v1.8.2)
**Goal**: Cartesian product of test parameters

- [x] `[Matrix(1, 2, 3)]` attribute for parameter values
- [x] Automatic Cartesian product generation across parameters
- [x] `[MatrixExclusion(1, "a")]` to skip specific combinations
- [ ] `[MatrixSourceMethod(nameof(Method))]` for dynamic values
- [ ] `[MatrixSourceRange(1, 10, step: 2)]` for numeric ranges

#### 2.2 Class Data Source

**Status**: Completed (v1.9.0)
**Goal**: Type-safe class-based test data

- [x] `[ClassDataSource<T>]` for single type
- [x] `[ClassDataSource<T1, T2>]` through `[ClassDataSource<T1, T2, T3, T4>]`
- [x] Shared/keyed instance support (SharedType: None, Keyed, PerClass, PerAssembly, PerSession)
- [x] AOT-compatible implementation

#### 2.3 Combined Data Sources

**Status**: Not Started
**Goal**: Mix different data sources per parameter

- [ ] `[CombinedDataSources]` to enable mixing
- [ ] Automatic Cartesian product of different sources
- [ ] Nested data source support

### Priority 3: Developer Experience

#### 3.1 Roslyn Analyzers

**Status**: Phase 1 Completed (v1.8.2)
**Goal**: Catch common mistakes at compile time

Phase 1 (Completed):

- [x] `NU0001`: Async void test methods (Warning) + Code Fix
- [x] `NU0002`: Test methods must be public (Error) + Code Fix
- [x] `NU0004`: Arguments count mismatch with parameters (Error)
- [x] `NU0006`: Timeout value must be positive (Error)

Phase 2 (Future):

- [ ] `NU0003`: Invalid data source references
- [ ] `NU0005`: Invalid lifecycle hook placement
- [ ] `NU0007`: DependsOn references non-existent test
- [ ] `NU0008`: Matrix parameters count mismatch

#### 3.2 Test Repeat Support

**Status**: Completed (v1.8.1)
**Goal**: Run tests multiple times

- [x] `[Repeat(count)]` attribute
- [x] Repeat index available via `TestContext`
- [ ] Aggregate results across repeats

#### 3.3 Test Execution Priority

**Status**: Not Started
**Goal**: Control test execution order

- [ ] `[ExecutionPriority(int)]` attribute
- [ ] Higher priority runs first
- [ ] Combine with `[DependsOn]` for complex ordering

#### 3.4 Explicit Tests

**Status**: Not Started
**Goal**: Tests only run when explicitly selected

- [ ] `[Explicit]` attribute to exclude from default runs
- [ ] `[Explicit("reason")]` with explanation
- [ ] Run with `--explicit` CLI flag

#### 3.5 Test Artifacts

**Status**: Not Started
**Goal**: Attach files to test results

- [ ] `TestContext.AttachArtifact(path)` method
- [ ] `TestContext.AttachArtifact(Artifact)` with metadata
- [ ] Support for screenshots, logs, videos
- [ ] Display in Test Explorer

#### 3.6 Watch Mode

**Status**: Not Started
**Goal**: Automatically re-run tests on file changes

- [ ] `--watch` CLI flag for continuous test execution
- [ ] Smart test selection (run affected tests only)
- [ ] File change debouncing
- [ ] Interactive filter during watch mode

### Priority 4: Ecosystem Integration

#### 4.1 ASP.NET Core Integration Package

**Status**: Not Started
**Goal**: First-class web application testing

- [ ] `NextUnit.AspNetCore` NuGet package
- [ ] `WebApplicationTest<TEntryPoint>` base class
- [ ] Auto-configured `HttpClient`
- [ ] Dependency injection in tests
- [ ] `TestWebApplicationFactory` utilities

#### 4.2 Playwright Integration Package

**Status**: Not Started
**Goal**: Browser testing support

- [ ] `NextUnit.Playwright` NuGet package
- [ ] `BrowserTest`, `ContextTest`, `PageTest` base classes
- [ ] Browser lifecycle management
- [ ] Screenshot capture on failure
- [ ] Trace recording

#### 4.3 Project Templates

**Status**: Not Started
**Goal**: Quick project scaffolding

- [ ] `NextUnit.Templates` NuGet package
- [ ] `dotnet new nextunit` - Basic test project
- [ ] `dotnet new nextunit-aspnet` - ASP.NET Core testing
- [ ] `dotnet new nextunit-playwright` - Browser testing

#### 4.4 .NET Aspire Testing Support

**Status**: Not Started
**Goal**: Distributed app testing

- [ ] Aspire AppHost integration
- [ ] Service dependency management
- [ ] Distributed tracing in test results
- [ ] Resource cleanup coordination

### Priority 5: Documentation & Community

#### 5.1 Migration Guides

**Status**: Partial
**Goal**: Easy migration from other frameworks

- [x] Migration from xUnit
- [ ] Migration from NUnit
- [ ] Migration from MSTest
- [ ] Automated migration tool (Roslyn-based)

#### 5.2 Sample Projects

**Status**: Partial
**Goal**: Real-world usage examples

- [x] Class library testing
- [x] Console app testing
- [ ] ASP.NET Core API testing
- [ ] Blazor component testing
- [ ] Minimal API testing
- [ ] gRPC service testing

#### 5.3 Documentation Site

**Status**: Not Started
**Goal**: Comprehensive documentation

- [ ] Docusaurus or similar static site
- [ ] API reference (auto-generated)
- [ ] Interactive examples
- [ ] Search functionality

### Priority 6: CI/CD Infrastructure

#### 6.1 Enhanced Benchmark Workflow

**Status**: Partial
**Goal**: Comprehensive performance tracking

- [x] Weekly benchmark workflow
- [ ] Daily benchmark execution
- [ ] Historical trend tracking (`historical.json`)
- [ ] AOT build benchmarks
- [ ] Automatic documentation generation from results
- [ ] Benchmark results in PR comments

#### 6.2 Security Scanning

**Status**: Not Started
**Goal**: Automated security analysis

- [ ] CodeQL workflow for security scanning
- [ ] Dependency vulnerability scanning
- [ ] SBOM generation

#### 6.3 Multi-Locale Testing

**Status**: Not Started
**Goal**: Ensure locale-independent behavior

- [ ] Test execution with different locales
- [ ] `[Culture("ja-JP")]` attribute for locale-specific tests
- [ ] `[InvariantCulture]` for locale-independent tests

---

## Implementation Timeline

### Q1 2026 (Current)

1. ~~Runtime test skipping~~ - **Completed** (v1.6.7)
2. ~~Timeout support~~ - **Completed** (v1.6.8)
3. ~~Test Context Injection~~ - **Completed** (v1.6.9)
4. ~~Retry support~~ - **Completed** (v1.6.9)
5. ~~Display name customization~~ - **Completed** (v1.7.0)
6. **Internal Refactoring** - In Progress (v1.7.x)
   - Critical: Code duplication elimination
   - High: Architecture improvements

### Q2 2026

1. ~~Enhanced parallel control~~ - **Completed** (v1.8.0)
2. ~~Test repeat support~~ - **Completed** (v1.8.1)
3. ~~Matrix data sources~~ - **Completed** (v1.8.2)
4. ~~Basic Roslyn analyzers~~ - **Phase 1 Completed** (v1.8.2)

### Q3 2026

1. ~~Class data sources~~ - **Completed** (v1.9.0)
2. Combined data sources
3. Test artifacts
4. ASP.NET Core integration package

### Q4 2026

1. Playwright integration
2. Project templates
3. Documentation site
4. Additional analyzers (20+ rules)

### 2027

1. .NET Aspire integration
2. Watch mode
3. Property-based testing (FsCheck integration)
4. Complete feature set

---

## Performance Benchmarks

| Framework | Per-Test Time | Tests/Sec | vs NextUnit  |
| --------- | ------------- | --------- | ------------ |
| NextUnit  | 2.77ms        | 361       | **Baseline** |
| MSTest    | 6.04ms        | 165       | 2.2x slower  |
| NUnit     | 6.28ms        | 159       | 2.3x slower  |
| xUnit     | 6.64ms        | 150       | 2.4x slower  |

**Note**: NextUnit's source-generator architecture significantly outperforms reflection-based frameworks.

**See**: [tools/speed-comparison/results/BENCHMARK_RESULTS.md](tools/speed-comparison/results/BENCHMARK_RESULTS.md)

---

## Contributing

We welcome contributions! Priority areas:

### Good First Issues

- Documentation improvements
- Sample projects
- Migration guides (NUnit, MSTest)

### Medium Complexity

- `[Explicit]` attribute
- Basic analyzers
- Test execution priority

### Advanced

- Matrix data sources
- Roslyn analyzers with code fixers
- ASP.NET Core integration
- Playwright integration

**See**: [README.md#contributing](README.md#contributing)

---

## Resources

- [Getting Started](docs/GETTING_STARTED.md)
- [Migration from xUnit](docs/MIGRATION_FROM_XUNIT.md)
- [Best Practices](docs/BEST_PRACTICES.md)
- [Performance Analysis](docs/PERFORMANCE.md)
- [CI/CD Integration](docs/CI_CD_INTEGRATION.md)

---

**Last Updated**: 2026-01-24
**Next Focus**: Priority 2.3 - Combined Data Sources, Priority 3.1 Phase 2 - Additional Analyzers
