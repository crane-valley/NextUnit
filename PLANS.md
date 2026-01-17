# NextUnit Development Roadmap

## Current Version: 1.6.6 (Stable)

NextUnit is a production-ready test framework for .NET 10+ with zero-reflection execution, rich assertions, and VSTest integration.

---

## Completed Features

| Version | Key Features |
|---------|-------------|
| 1.6.x | VSTest adapter, Visual Studio Test Explorer, rich failure messages, advanced CLI filtering |
| 1.5.x | Predicate-based collection assertions, xUnit API compatibility |
| 1.4.x | Performance benchmarks, BenchmarkDotNet integration |
| 1.3.x | Test output capture (ITestOutput) |
| 1.2.x | CLI arguments, session lifecycle |
| 1.1.x | Category and tag filtering |
| 1.0.x | Zero-reflection execution, multi-scope lifecycle, parallel control |

**See**: [CHANGELOG.md](CHANGELOG.md) for complete version history.

---

## Upcoming Features

### Priority 1: Core Enhancements

#### 1.1 Runtime Test Skipping
**Status**: Not Started
**Goal**: Allow conditional test skipping at runtime

- `Assert.Skip(reason)` - Skip test during execution with reason
- `Assert.SkipWhen(condition, reason)` - Conditional skip
- Platform-specific skipping helpers (`SkipOnWindows`, `SkipOnLinux`, etc.)
- Skip reason displayed in test results

#### 1.2 Test Context Injection
**Status**: Not Started
**Goal**: Provide runtime test information to tests

- `ITestContext` interface with test name, class, assembly info
- Inject via constructor alongside `ITestOutput`
- Access to test properties, categories, and tags at runtime
- Current test timeout and cancellation token

#### 1.3 Retry and Flaky Test Support
**Status**: Not Started
**Goal**: Handle intermittent test failures gracefully

- `[Retry(count)]` attribute for automatic retry on failure
- `[Flaky]` attribute to mark known flaky tests
- Retry statistics in test reports
- Configurable retry delay

#### 1.4 Timeout Support
**Status**: Not Started
**Goal**: Prevent hung tests from blocking execution

- `[Timeout(milliseconds)]` attribute per test
- Class-level and assembly-level timeout defaults
- Graceful cancellation with cleanup
- Timeout warning threshold

### Priority 2: Developer Experience

#### 2.1 Watch Mode
**Status**: Not Started
**Goal**: Automatically re-run tests on file changes

- `--watch` CLI flag for continuous test execution
- Smart test selection (run affected tests only)
- File change debouncing
- Interactive filter during watch mode

#### 2.2 Code Coverage Integration
**Status**: Not Started
**Goal**: Built-in code coverage support

- Integration with coverlet
- `--coverage` CLI flag
- Coverage report generation (Cobertura, OpenCover)
- Coverage thresholds and enforcement

#### 2.3 HTML/JSON Report Generation
**Status**: Not Started
**Goal**: Rich test reports without external tools

- `--report html` for standalone HTML report
- `--report json` for machine-readable output
- Test execution timeline visualization
- Failure screenshots/attachments support

#### 2.4 Performance Regression Detection
**Status**: Not Started
**Goal**: Catch performance degradation early

- `[Benchmark]` attribute for performance-critical tests
- Baseline comparison with previous runs
- Configurable tolerance thresholds
- Integration with BenchmarkDotNet

### Priority 3: Ecosystem Integration

#### 3.1 .NET Aspire Testing Support
**Status**: Not Started
**Goal**: First-class support for distributed app testing

- Aspire AppHost integration
- Service dependency management
- Distributed tracing in test results
- Resource cleanup coordination

#### 3.2 Container Testing Support
**Status**: Not Started
**Goal**: Simplified Docker/container testing

- `[RequiresDocker]` attribute
- Testcontainers integration examples
- Container lifecycle management
- Port mapping and networking helpers

#### 3.3 Database Testing Helpers
**Status**: Not Started
**Goal**: Simplify database integration testing

- Transaction rollback per test
- In-memory database auto-configuration
- Database seeding utilities
- EF Core integration patterns

#### 3.4 HTTP Testing Utilities
**Status**: Not Started
**Goal**: Streamlined API testing

- `WebApplicationFactory` integration
- Request/response assertion helpers
- Mock HTTP handler utilities
- OpenAPI contract testing

### Priority 4: Advanced Features

#### 4.1 Test Data Generators
**Status**: Not Started
**Goal**: Reduce boilerplate for test data creation

- `[AutoGenerate]` attribute for auto-generated test data
- Faker/Bogus integration
- Custom generator support
- Reproducible random seeds

#### 4.2 Snapshot Testing
**Status**: Not Started
**Goal**: Verify complex output against stored snapshots

- `Assert.MatchesSnapshot(value)` for JSON/text/object comparison
- Automatic snapshot creation on first run
- Snapshot update workflow
- Diff visualization on mismatch

#### 4.3 Parallel Test Collections
**Status**: Not Started
**Goal**: Fine-grained parallelism control

- `[TestCollection("name")]` to group tests
- Collections run serially with each other
- Per-collection parallel limits
- Resource isolation between collections

#### 4.4 Test Ordering Extensions
**Status**: Not Started
**Goal**: More flexible test ordering options

- `[RunFirst]` / `[RunLast]` attributes
- Priority-based ordering
- Alphabetical ordering option
- Random ordering for isolation verification

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

#### 5.3 Video Tutorials
**Status**: Not Started
**Goal**: Visual learning resources

- Getting started walkthrough
- Advanced features deep dive
- Migration from xUnit tutorial
- Performance optimization guide

---

## Implementation Timeline

### Near-Term (Next Release)
1. Runtime test skipping (`Assert.Skip`)
2. Timeout support
3. Watch mode (basic)

### Mid-Term (Q2 2026)
1. Test context injection
2. Retry support
3. Code coverage integration
4. HTML/JSON reports

### Long-Term (Q3-Q4 2026)
1. .NET Aspire integration
2. Snapshot testing
3. Auto-generated test data
4. Migration tool

---

## Performance Benchmarks

| Framework | Per-Test Time | Tests/Sec | vs NextUnit |
|-----------|---------------|-----------|-------------|
| NextUnit  | 2.77ms        | 361       | **Baseline** |
| MSTest    | 6.04ms        | 165       | 2.2x slower |
| NUnit     | 6.28ms        | 159       | 2.3x slower |
| xUnit     | 6.64ms        | 150       | 2.4x slower |

**See**: [tools/speed-comparison/results/BENCHMARK_RESULTS.md](tools/speed-comparison/results/BENCHMARK_RESULTS.md)

---

## Contributing

We welcome contributions! Priority areas:

1. **Good First Issues**: Documentation improvements, sample projects
2. **Medium**: New assertion methods, CLI enhancements
3. **Advanced**: Watch mode, code coverage integration

**See**: [README.md#contributing](README.md#contributing)

---

## Resources

- [Getting Started](docs/GETTING_STARTED.md)
- [Migration from xUnit](docs/MIGRATION_FROM_XUNIT.md)
- [Best Practices](docs/BEST_PRACTICES.md)
- [Performance Analysis](docs/PERFORMANCE.md)
- [CI/CD Integration](docs/CI_CD_INTEGRATION.md)

---

**Last Updated**: 2026-01-18
**Next Focus**: Runtime test skipping, timeout support
