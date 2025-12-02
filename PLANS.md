## NextUnit Implementation Plan

### Motivation & Background

NextUnit was born from the desire to combine the best features of modern .NET testing frameworks while maintaining familiar assertion patterns. Specifically:

**What we love about TUnit and want to keep:**
- ✅ **Microsoft.Testing.Platform integration** - Native support for modern IDE tooling and CLI experiences
- ✅ **Native AOT compatibility** - Source generator-based discovery with zero runtime reflection
- ✅ **Instance-per-test-case parallelism** - Each test gets a fresh instance, maximizing parallel execution by default
- ✅ **Fine-grained orchestration** - Rich control attributes: `[ParallelLimit]`, `[NotInParallel]`, `[DependsOn]`, `[Before]`, `[After]`
- ✅ **Behavior-focused attribute naming** - Attributes describe what happens (`[Before]`, `[After]`) rather than abstract concepts
  - We reject confusing names like `[Fact]` (what does "fact" mean in testing?) or `[Theory]` (is this philosophy class?)
  - NextUnit uses clear, action-oriented names: `[Test]` means "this is a test" - simple and obvious

**What we want to change from TUnit:**
- ❌ **Fluent assertion API (`Assert.That`)** - TUnit's fluent syntax is unique but diverges from established conventions
  - NextUnit provides: Classic `Assert.Equal(expected, actual)` matching xUnit/NUnit/MSTest
- ❌ **All assertions require await** - TUnit makes all assertions async, even for simple value checks
  - NextUnit provides: Synchronous APIs for sync scenarios; separate async helpers (`ThrowsAsync`, etc.) when needed

**The Gap NextUnit Fills:**

Existing frameworks have trade-offs:
- **xUnit/NUnit/MSTest** - Familiar assertions but rely on runtime reflection, struggle with Native AOT, lack advanced orchestration
- **TUnit** - Excellent platform integration and orchestration but unfamiliar assertion patterns that slow adoption

NextUnit bridges this gap: **TUnit's modern architecture + xUnit's ergonomic assertions = a framework that feels familiar while being forward-compatible with .NET's future.**

### Vision
- Deliver a lightweight, **high-performance** test framework that combines the best of TUnit (Microsoft.Testing.Platform integration, Native AOT support via source generators, instance-per-test-case parallelism, fine-grained orchestration) with xUnit-style assertion ergonomics (simple synchronous APIs, familiar attribute names, no fluent syntax).
- Deep integration with Microsoft.Testing.Platform ensures compatibility with modern IDE tooling, command-line filters, and standardized reporting.
- **Target .NET 10+ exclusively** - embrace the latest runtime features and performance optimizations without legacy compatibility burden.
- **Zero reflection in production code** - use source generators for all test discovery and metadata collection, ensuring blazing-fast startup and full Native AOT compatibility.
- **Low maintenance cost** - simple, focused codebase with clear separation of concerns and comprehensive automated testing.

### Guiding Principles
- **Deterministic orchestration:** Honor declared dependencies, lifecycle scopes, and parallel constraints exactly; fail fast when specifications cannot be satisfied.
- **Developer empathy:** Minimize ceremony, provide actionable error messages, align naming with xUnit conventions where practical.
- **Incremental delivery:** Ship vertical slices (discovery → execution → reporting) to enable early dogfooding and feedback.
- **Maintainability:** Prefer explicit contracts (descriptors, registries, message protocols) over ad-hoc reflection.
- **Performance-first architecture:** Eliminate System.Reflection usage in production code paths; rely exclusively on source generator for test discovery to achieve maximum performance and Native AOT compatibility.
- **English-only codebase:** All code, comments, documentation, and commit messages must be in English to ensure international collaboration and consistency with .NET ecosystem standards (see [CODING_STANDARDS.md](CODING_STANDARDS.md)).

### Current Status (2025-12-02)

#### Completed Work
- ✅ Core attribute definitions (`[Test]`, `[Before]`, `[After]`, `[DependsOn]`, `[NotInParallel]`, `[ParallelLimit]`)
- ✅ Basic assertion library with common operations (`True`, `False`, `Equal`, `NotEqual`, `Null`, `NotNull`, `Throws`, `ThrowsAsync`)
- ✅ Test descriptor model (`TestCaseDescriptor`, `LifecycleInfo`, `ParallelInfo`) with delegate-based execution
- ✅ Dependency graph builder with cycle detection
- ✅ Basic execution engine with lifecycle hooks and proper `IDisposable`/`IAsyncDisposable` support
- ✅ Delegate-based test and lifecycle method invocation (zero reflection in execution path)
- ✅ **Source generator emitting complete test registry with delegates**
- ✅ **Generator diagnostics for dependency validation**
- ✅ Microsoft.Testing.Platform registration infrastructure
- ✅ Sample test suite with 20 tests demonstrating core features
- ✅ All sample tests passing (20/20 success rate)

#### Known Gaps  
- ⚠️ **Reflection fallback active for discovery** - generator works but fallback still in code path (#if false)
- ❌ Generator unit tests not yet written
- ❌ Generator performance benchmarks not yet performed
- ❌ `ParallelScheduler` only implements dependency ordering; parallel limits not enforced
- ❌ Lifecycle scopes beyond `Test` (Assembly, Class, Session, Discovery) not implemented
- ❌ No skip propagation when dependencies fail
- ❌ Missing test result aggregation and reporting enhancements

#### Recent Progress (Session 2025-12-02 Update)
- ✅ **M1 Major Progress** - Source generator now emits complete test registry with delegates
- ✅ Implemented delegate-based test method invocation (no reflection in execution)
- ✅ Implemented delegate-based lifecycle method invocation (no reflection in execution)
- ✅ Added helper methods to generated code for method signature variations
- ✅ Generator diagnostics added: cycle detection (NEXTUNIT001), unresolved dependencies (NEXTUNIT002)
- ✅ Validated generated code compiles and runs correctly (all 20 tests pass)
- ✅ Generated code includes lifecycle hooks (Setup/Teardown methods)
- ✅ Generated code properly resolves dependencies
- 📝 Reflection fallback still present but clearly marked with TODO for removal
