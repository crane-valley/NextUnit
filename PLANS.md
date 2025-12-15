# NextUnit Development Plans

## Status: Implementation Complete ✅

The speed-comparison system has been **successfully implemented** with enhancements beyond the original plan. See [Implementation Status](#implementation-status-speed-comparison-system) below.

## Overview

This document tracks completed implementations and outlines the development roadmap for NextUnit going forward.

---

## Implementation Status: Speed-Comparison System

### ✅ Completed Features

**Completion Date**: 2025-12-10

The speed-comparison benchmarking system is **fully operational** with the following achievements:

#### 1. Architecture Improvements ✅
- **BenchmarkDotNet Integration**: Professional statistical analysis with mean, median, std dev
- **UnifiedTests Project**: Single codebase using conditional compilation (no code duplication)
- **127 Tests per Framework**: Comprehensive test coverage across 6 categories
- **Native AOT Support**: NextUnit-specific AOT compilation benchmarks

#### 2. Implementation Delivered ✅
- ✅ **Fair Comparison**: Identical test logic via conditional compilation
- ✅ **Comprehensive Metrics**: Build time, execution time, statistical analysis
- ✅ **Automated Updates**: GitHub Actions workflow with weekly scheduling
- ✅ **Transparency**: Complete documentation in tools/speed-comparison/
- ✅ **Results Publishing**: BENCHMARK_RESULTS.md with latest comparisons

#### 3. Key Differences from Original Plan
The implementation **exceeds** the original plan with:
- **Better Architecture**: UnifiedTests (conditional compilation) vs. separate projects
- **Professional Benchmarking**: BenchmarkDotNet vs. custom stopwatch-based runner
- **Build Benchmarks**: Compilation time measurement (not in original plan)
- **AOT Support**: Native AOT benchmarks for NextUnit (not in original plan)

### Current Performance Results

From latest benchmarks (2025-12-10):

| Framework | Version | Per-Test Time | Tests/Sec | Relative Performance |
|-----------|---------|---------------|-----------|---------------------|
| NextUnit  | 1.5.0   | 2.77ms        | 361       | **Baseline**        |
| MSTest    | 3.7.0   | 6.04ms        | 165       | 2.2x slower         |
| NUnit     | 4.3.1   | 6.28ms        | 159       | 2.3x slower         |
| xUnit     | 2.9.3   | 6.64ms        | 150       | 2.4x slower         |

**See**: [tools/speed-comparison/results/BENCHMARK_RESULTS.md](tools/speed-comparison/results/BENCHMARK_RESULTS.md)

---

## Forward-Looking Development Roadmap

This section outlines planned improvements and enhancements for NextUnit going forward.

### Priority 1: Core Framework Enhancements

#### 1.1 Enhanced Assertions and Diagnostics
**Goal**: Improve developer experience with better error messages and more assertion types

**Planned Features**:
- **Approximate equality assertions** for floating-point comparisons
  - `Assert.Equal(expected, actual, precision)` - Compare doubles/decimals with tolerance
  - `Assert.NotEqual(notExpected, actual, precision)` - Inverse with tolerance
  - Common in scientific computing and financial applications

- **Collection comparison assertions**
  - `Assert.Equivalent(expected, actual)` - Unordered collection equality
  - `Assert.Subset(subset, superset)` - Verify subset relationship
  - `Assert.Disjoint(collection1, collection2)` - No common elements

- **Better exception assertions**
  - `Assert.Throws<T>(action, expectedMessage)` - Match exception message
  - Async variants with message matching
  - Support for InnerException validation

- **Custom comparers support**
  - `Assert.Equal(expected, actual, IEqualityComparer<T>)`
  - Allow custom equality logic for complex types

**Estimated Effort**: 2-3 days
**Benefits**: Reduces boilerplate, improves test readability, matches xUnit/NUnit feature parity

#### 1.2 Test Context and Environment
**Goal**: Provide tests with runtime information and configuration

**Planned Features**:
- **Test context injection** (similar to NUnit's TestContext)
  - `ITestContext` interface with test name, class, assembly info
  - Current test status, start time, runtime
  - Test properties and metadata access

- **Environment-based test configuration**
  - Conditional test execution based on environment variables
  - Platform-specific test skipping (Windows-only, Linux-only)
  - Runtime version checks

- **Test data from configuration files**
  - Load test data from JSON/YAML/XML files
  - Support for external test data sources
  - Configuration-driven parameterized tests

**Estimated Effort**: 3-4 days
**Benefits**: Better integration testing support, environment-aware tests, reduced hardcoding

#### 1.3 Advanced Parallel Execution Features
**Goal**: Fine-tune parallel execution for complex scenarios

**Planned Features**:
- **Resource-based parallelization**
  - `[RequiresResource("database")]` - Serialize tests needing same resource
  - Resource pools with max concurrency
  - Automatic deadlock prevention

- **Test prioritization**
  - `[Priority(1)]` - Run high-priority tests first
  - Fast tests before slow tests
  - Critical path optimization

- **Parallel test collections**
  - Group tests into collections that don't run in parallel with each other
  - Similar to xUnit collections but more flexible
  - `[TestCollection("DatabaseTests")]`

**Estimated Effort**: 4-5 days
**Benefits**: Better resource management, faster feedback loops, reduced test flakiness

### Priority 2: Developer Tooling and IDE Integration

#### 2.1 Visual Studio 2026 Integration
**Goal**: First-class Visual Studio 2026 Test Explorer support

**Status**: ✅ **Basic Test Explorer Support Implemented** (2025-12-15)

**Completed Features**:
- ✅ **Test Explorer adapter for Visual Studio 2026**
  - Show tests in VS Test Explorer via native TRX capability (ITrxReportCapability)
  - Run/debug individual tests from IDE
  - Full integration with Microsoft.Testing.Platform (no VSTest compatibility layer)

**Future Enhancements**:
- **Live Unit Testing support**
  - Real-time test execution during coding
  - Show pass/fail indicators in code editor
  - Automatic re-run on code changes

- **CodeLens integration**
  - Test count and status above test classes
  - "Run Test" / "Debug Test" links
  - Last execution time and result

- **Additional Test Framework Capabilities**
  - BannerCapability for custom test framework branding
  - StopExecutionCapability for fail-fast behavior
  - Additional capabilities as needed for enhanced IDE integration

**Estimated Effort for Enhancements**: 3-5 days
**Benefits**: Better developer experience, faster development workflow, IDE parity with xUnit

**Technical Implementation Details**:
- Uses `ITrxReportCapability` from `Microsoft.Testing.Extensions.TrxReport.Abstractions`
- Registered through `ITestFrameworkCapabilities.Capabilities` collection
- Follows TUnit's architecture pattern for native Microsoft.Testing.Platform integration
- No VSTest compatibility layer - uses modern testing infrastructure directly

**Note**: NextUnit targets .NET 10, which requires Visual Studio 2026 for development.

#### 2.2 Debugging and Diagnostics
**Goal**: Make debugging test failures easier

**Planned Features**:
- **Rich failure messages**
  - Visual diff for string/collection mismatches
  - Highlight differences in expected vs actual
  - JSON-formatted output for complex objects

- **Test execution timeline**
  - Visualize test execution order
  - Show parallel execution patterns
  - Identify bottlenecks and slow tests

- **Performance profiling integration**
  - Built-in performance warnings for slow tests
  - Automatic detection of performance regressions
  - Memory leak detection

**Estimated Effort**: 3-4 days
**Benefits**: Faster debugging, better visibility into test execution, proactive performance monitoring

#### 2.3 CLI Improvements
**Goal**: Enhanced command-line experience

**Planned Features**:
- **Interactive mode**
  - Watch mode: re-run tests on file changes
  - Interactive filter selection
  - Live test results

- **Advanced filtering**
  - Regular expression matching for test names
  - Complex filter expressions (AND/OR/NOT)
  - Saved filter profiles

- **Better reporting formats**
  - JUnit XML output
  - TeamCity service messages
  - Azure DevOps format
  - GitHub Actions annotations

**Estimated Effort**: 2-3 days
**Benefits**: CI/CD integration, better local development workflow, team collaboration

### Priority 3: Extensibility and Ecosystem

#### 3.1 Extension Points and Plugins
**Goal**: Allow community to extend NextUnit functionality

**Planned Features**:
- **Custom assertion libraries**
  - Plugin API for third-party assertion packages
  - FluentAssertions-style syntax option
  - Domain-specific assertion helpers

- **Custom test discovery**
  - Allow custom attributes for test discovery
  - Convention-based test discovery
  - External test metadata sources

- **Test execution hooks**
  - Global before/after test execution
  - Custom test result reporters
  - Test retry policies

**Estimated Effort**: 4-5 days
**Benefits**: Community contributions, specialized testing scenarios, flexibility

#### 3.2 Integration with Popular Libraries
**Goal**: Seamless integration with common testing tools

**Planned Features**:
- **Mocking frameworks integration**
  - Moq, NSubstitute, FakeItEasy integration examples
  - Auto-mocking support
  - Verify helpers for common patterns

- **AutoFixture integration**
  - Automatic test data generation
  - `[AutoData]` attribute support
  - Customization support

- **Database testing support**
  - Entity Framework integration
  - In-memory database helpers
  - Transaction rollback support

- **HTTP testing helpers**
  - WebApplicationFactory integration
  - HTTP client mocking
  - API testing utilities

**Estimated Effort**: 3-4 days
**Benefits**: Lower barrier to adoption, common use cases covered, reduce boilerplate

#### 3.3 Documentation and Samples
**Goal**: Comprehensive learning resources

**Planned Features**:
- **Sample projects**
  - ASP.NET Core API testing
  - Blazor component testing
  - Console app testing
  - Class library testing

- **Migration guides**
  - From xUnit (already exists, enhance)
  - From NUnit
  - From MSTest
  - Migration automation tools

- **Video tutorials**
  - Getting started
  - Advanced features
  - Best practices
  - Performance optimization

- **API documentation**
  - Complete XML docs
  - DocFX site generation
  - Interactive examples
  - Search functionality

**Estimated Effort**: 5-7 days
**Benefits**: Faster adoption, reduced support burden, community growth

### Priority 4: Performance and Optimization

#### 4.1 Continued Performance Improvements
**Goal**: Maintain industry-leading performance

**Planned Features**:
- **Assembly startup optimization**
  - Reduce framework initialization time
  - Lazy loading of optional features
  - Optimized dependency resolution

- **Memory allocation reduction**
  - Pool test context objects
  - Reduce allocations in hot paths
  - GC pressure optimization

- **Parallel execution optimization**
  - Work-stealing scheduler
  - Better CPU utilization
  - Adaptive parallelism based on load

**Estimated Effort**: 3-4 days
**Benefits**: Faster test execution, lower resource usage, better scalability

#### 4.2 Native AOT Enhancements
**Goal**: Best-in-class Native AOT support

**Planned Features**:
- **Full Native AOT compatibility**
  - Eliminate remaining reflection
  - AOT-friendly serialization
  - Trim warnings resolution

- **AOT performance optimization**
  - Startup time reduction
  - Binary size optimization
  - Memory footprint improvements

- **AOT documentation**
  - Setup guides
  - Limitations and workarounds
  - Best practices

**Estimated Effort**: 3-4 days
**Benefits**: Serverless/edge deployment scenarios, faster startup, smaller binaries

#### 4.3 Benchmark Expansion
**Goal**: Comprehensive performance tracking

**Planned Features**:
- **More framework comparisons**
  - Add TUnit to benchmarks
  - Add Fixie to benchmarks
  - Document why frameworks differ

- **Platform-specific benchmarks**
  - Windows vs Linux vs macOS
  - ARM vs x64
  - .NET 8 vs .NET 9 vs .NET 10

- **Real-world scenario benchmarks**
  - Integration test scenarios
  - Database-heavy test suites
  - API testing scenarios

- **Historical trend analysis**
  - Track performance over versions
  - Automated regression detection
  - Performance badges

**Estimated Effort**: 2-3 days
**Benefits**: Performance transparency, regression prevention, competitive positioning

### Priority 5: Community and Ecosystem Growth

#### 5.1 Community Building
**Goal**: Build an active contributor community

**Planned Activities**:
- **Contributor guidelines**
  - Clear contribution process
  - Code review guidelines
  - Issue triage process
  - Recognition program

- **Community channels**
  - GitHub Discussions
  - Discord/Slack channel
  - Stack Overflow tag
  - Regular community calls

- **Good first issues**
  - Label beginner-friendly tasks
  - Provide mentorship
  - Celebrate first contributions

**Estimated Effort**: Ongoing
**Benefits**: Faster development, diverse perspectives, sustainability

#### 5.2 Marketing and Awareness
**Goal**: Increase NextUnit adoption

**Planned Activities**:
- **Blog posts and articles**
  - Technical deep dives
  - Performance comparisons
  - Migration stories
  - Best practices

- **Conference talks**
  - .NET Conf presentations
  - Local user group talks
  - Online webinars
  - Demo videos

- **Social media presence**
  - Twitter/X updates
  - LinkedIn articles
  - Reddit discussions
  - Dev.to posts

**Estimated Effort**: Ongoing
**Benefits**: Increased adoption, community feedback, industry recognition

#### 5.3 Enterprise Features
**Goal**: Make NextUnit suitable for large organizations

**Planned Features**:
- **Test reporting and analytics**
  - Test history database
  - Trend analysis
  - Flaky test detection
  - Team dashboards

- **Test organization**
  - Test suites and groups
  - Test ownership metadata
  - Required approvals for critical tests
  - Test governance policies

- **CI/CD platform integrations**
  - Azure DevOps native support
  - GitHub Actions native support
  - Jenkins plugin
  - GitLab CI integration

**Estimated Effort**: 7-10 days
**Benefits**: Enterprise adoption, team collaboration, better visibility

---

## Development Timeline and Priorities

### Near-Term (Next 1-3 Months)
**Focus**: Core framework enhancements and developer experience

1. ✅ **Enhanced Assertions** (Priority 1.1) - COMPLETED (2025-12-14)
   - Approximate equality assertions for doubles/decimals
   - Collection comparison assertions (Equivalent, Subset, Disjoint)
   - Exception message matching
   - Custom comparers support
2. ✅ **Visual Studio Test Explorer Support** (Priority 2.1 - Basic) - COMPLETED (2025-12-15)
   - Native TRX capability integration (ITrxReportCapability)
   - Tests discoverable in Visual Studio Test Explorer
   - Run/debug tests from IDE
   - No VSTest compatibility layer - uses Microsoft.Testing.Platform directly
3. **CLI Improvements** (Priority 2.3) - 2-3 days
4. **Rich Failure Messages** (Priority 2.2) - 1-2 days
5. **Documentation Samples** (Priority 3.3) - 3-4 days

**Total**: ~7-9 days remaining

### Mid-Term (3-6 Months)
**Focus**: Advanced IDE integration and tooling

1. **Advanced Visual Studio Integration** (Priority 2.1 - Enhanced) - 3-5 days
   - Live Unit Testing support
   - CodeLens integration
2. **Test Context and Environment** (Priority 1.2) - 3-4 days
3. **Extension Points** (Priority 3.1) - 4-5 days
4. **Library Integrations** (Priority 3.2) - 3-4 days

**Total**: ~13-18 days of development

### Long-Term (6-12 Months)
**Focus**: Enterprise features and ecosystem growth

1. **Advanced Parallel Execution** (Priority 1.3) - 4-5 days
2. **Enterprise Features** (Priority 5.3) - 7-10 days
3. **Community Building** (Priority 5.1) - Ongoing
4. **Marketing and Awareness** (Priority 5.2) - Ongoing

**Total**: ~11-15 days of development + ongoing activities

### Continuous Efforts
- Performance optimization (Priority 4.1) - Ongoing
- Native AOT improvements (Priority 4.2) - Ongoing
- Benchmark expansion (Priority 4.3) - Ongoing
- Documentation updates - Ongoing
- Community support - Ongoing

---

## Success Metrics

### Adoption Metrics
- **Downloads**: Track NuGet package downloads
- **GitHub Stars**: Monitor repository popularity
- **Community Size**: Discord/discussions participation
- **Migration Rate**: Users migrating from other frameworks

### Quality Metrics
- **Performance**: Maintain 2x+ speed advantage over competitors
- **Stability**: < 1% test flakiness rate
- **Documentation Coverage**: 100% API documentation
- **Test Coverage**: > 90% code coverage

### Community Health
- **Contributor Count**: Active contributors per quarter
- **Issue Response Time**: < 48 hours for initial response
- **PR Merge Time**: < 7 days for reviewed PRs
- **Community Sentiment**: Positive feedback ratio

---

## Contributing to NextUnit

We welcome contributions! Here's how you can help:

### Code Contributions
- Pick an issue labeled "good first issue" or "help wanted"
- Follow the coding conventions in `.editorconfig`
- Add tests for new features
- Update documentation for public APIs

### Documentation Contributions
- Improve getting started guides
- Add code samples and examples
- Fix typos and clarify confusing sections
- Translate documentation (future)

### Community Contributions
- Answer questions on GitHub Discussions
- Write blog posts about NextUnit
- Share your migration stories
- Report bugs and suggest features

**See**: [Contributing Guidelines](README.md#contributing) for detailed guidelines

---

## Resources and References

### Current Documentation
- [README.md](README.md) - Project overview and quick start
- [GETTING_STARTED.md](docs/GETTING_STARTED.md) - Complete beginner guide
- [MIGRATION_FROM_XUNIT.md](docs/MIGRATION_FROM_XUNIT.md) - xUnit migration guide
- [BEST_PRACTICES.md](docs/BEST_PRACTICES.md) - Recommended patterns
- [PERFORMANCE.md](docs/PERFORMANCE.md) - Performance benchmarks and analysis

### Speed Comparison System
- [Speed Comparison README](tools/speed-comparison/README.md) - How to use benchmarks
- [Benchmark Results](tools/speed-comparison/results/BENCHMARK_RESULTS.md) - Latest performance data
- [Implementation Summary](tools/speed-comparison/IMPLEMENTATION_SUMMARY.md) - Architecture details

### External Resources
- [Microsoft.Testing.Platform Documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [TUnit Framework](https://github.com/thomhurst/TUnit) - Inspiration for architecture
- [xUnit](https://xunit.net/) - Inspiration for assertions

---

## Version History

| Version | Date       | Status      | Highlights |
|---------|------------|-------------|------------|
| 1.7.0   | 2025-12-15 | ✅ Released | Visual Studio Test Explorer support via native TRX capability |
| 1.6.1   | 2025-12-15 | ✅ Released | Package configuration: DevelopmentDependency=true for all packages |
| 1.6.0   | 2025-12-14 | ✅ Released | Enhanced assertions (Priority 1.1): precision equality, collection comparisons, message matching |
| 1.5.0   | 2025-12-10 | ✅ Released | Predicate-based collection assertions |
| 1.4.0   | 2025-12-09 | ✅ Released | Performance benchmarks, BenchmarkDotNet integration |
| 1.3.0   | 2025-12-08 | ✅ Released | Test output capture (ITestOutput) |
| 1.2.0   | 2025-12-07 | ✅ Released | CLI arguments, session lifecycle |
| 1.1.0   | 2025-12-06 | ✅ Released | Category and tag filtering |
| 1.0.0   | 2025-12-06 | ✅ Released | First stable release |

**See**: [CHANGELOG.md](CHANGELOG.md) for complete version history

---

## Contact and Support

- **GitHub Issues**: [Report bugs and request features](https://github.com/crane-valley/NextUnit/issues)
- **GitHub Discussions**: [Ask questions and share ideas](https://github.com/crane-valley/NextUnit/discussions)
- **Email**: (to be added)
- **Discord**: (to be created)

---

**Last Updated**: 2025-12-15  
**Document Version**: 2.2  
**Status**: Priority 2.1 (Visual Studio Test Explorer - Basic) complete, continuing Near-Term roadmap
