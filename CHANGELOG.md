# Changelog

All notable changes to NextUnit will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Removed

- Remove the `Assert.Throws<TException>(Action, string expectedMessage, string? message)` and
  `Assert.ThrowsAsync<TException>(Func<Task>, string expectedMessage, string? message)` overloads,
  `[Obsolete]` since 1.17.0. This is a breaking change, and the next release is 2.0.0. Assert on the
  returned exception's `Message` instead, which is what the deprecation notice directed callers to.
  A call that passes two positional arguments is unaffected: it has always bound to the
  `(Action, string? message)` custom-message overload, because overload resolution prefers the
  candidate that leaves no optional parameter unfilled. The removed overloads were therefore
  reachable only through an explicit third argument or a named `expectedMessage:` argument, and those
  two forms are what stops compiling. An assembly compiled against the 1.x overloads has to be
  rebuilt.

### Fixed

- Resolve `[ParallelLimit]` from the assembly. `ParallelLimitAttribute` has always accepted an
  assembly target, but the generator read the limit from the test method and its containing class
  only, so `[assembly: ParallelLimit(4)]` compiled and was then dropped and the run fell back to the
  processor count. The limit now resolves method first, then class, then assembly, matching
  `[Timeout]` and the culture attributes. A suite that declares no limit anywhere is unaffected.

### Added

- `NU0019` rejects a non-positive `[ParallelLimit]` value. The value becomes
  `ParallelOptions.MaxDegreeOfParallelism`, whose setter throws for `0` and for anything below `-1`,
  and that throw aborts the whole run rather than failing the test that declared it; resolving the
  attribute from the assembly turned a previously inert `[assembly: ParallelLimit(0)]` into exactly
  that. `-1` is rejected as well: the setter accepts it, but `Parallel.ForEachAsync` maps it to the
  processor count, which is what an absent attribute already means, while it still wins the `Min`
  the scheduler takes across a parallel group, replacing a sibling's explicit limit with a processor
  count that may be higher. The rule covers the method, class, and assembly forms alike, and the generator
  drops a value it reports so that a suppressed error bounds the run by the enclosing declaration
  instead.

## [1.19.1] - 2026-08-10

### Fixed

- Escape keyword identifiers in every emitted type name. A test class, data source type, or parameter
  type whose identifier is an escaped keyword (`public class @event`) reached the generated registry
  as `global::event`, which does not parse, so the consumer's build failed with errors pointing at a
  file the user did not write. Test ids and display names are unaffected: they are string literals,
  so they keep the unescaped spelling they already had.
- Mark the test nodes published by discovery with `DiscoveredTestNodeStateProperty`.
  Microsoft.Testing.Platform counts only nodes that carry it, so `--list-tests` reported
  `found 0 test(s)` and exited with code 8 on assemblies whose ordinary run discovered and passed
  every test, and any IDE that discovers through the platform rather than the VSTest adapter saw the
  same empty assembly. The run path is unchanged, and a deferred `[TestData]` source still lists as
  one placeholder because its rows do not exist until the run expands them.

## [1.19.0] - 2026-08-05

### Added

- `[TestData]` binds a member that yields its rows asynchronously: `IAsyncEnumerable<T>`, which may
  take the cancellation token, and `Task<TCollection>` or `ValueTask<TCollection>`. The generator
  classifies the member's shape and emits a typed provider with the element type bound statically, so
  the path neither reflects nor instantiates a generic at runtime and stays usable under trimming and
  Native AOT. A synchronous collection still wins the classification, so an existing source that also
  implements `IAsyncEnumerable<T>` keeps its meaning, and rows are still materialized at discovery, so
  each row remains an individually selectable and filterable test case. Cancellation is observed at
  every await point rather than only between rows -- a pending move and the enumerator's disposal are
  each raced against the token -- and rows are never returned to a caller whose token was cancelled.
  The contract that follows is documented on `TestDataAttribute` and in `docs/GETTING_STARTED.md`: a
  data source must not block its calling thread, because no token can interrupt a thread that never
  reaches an await.
- `NU0014` reports an awaitable `[TestData]` member the generated path cannot read rows from, such as
  a bare `Task`. The reflection fallback is deliberately synchronous, because reading an
  `IAsyncEnumerable<T>` reflectively needs runtime generic instantiation that trimming cannot see, so
  the statically detectable cases are reported at build time instead of failing at run time.
- `DeferredEnumeration = true` on `[TestData]` moves a member's enumeration from discovery to
  execution, for a source large or slow enough that reading it at startup is itself the problem.
  Discovery reports one placeholder per source and never calls the member, and the execution engine
  replaces that placeholder with the real rows before it builds the dependency graph, so the graph,
  the scheduler, retry, and reporting all see ordinary test cases. Filtering is group-level by design:
  a deferred source is filtered by the test method's own name, categories, and tags, never by row
  metadata that does not exist yet, and no filter silently restores eager enumeration. A source that
  fails is reported on its own placeholder instead of aborting the run, and one that yields no rows is
  reported as skipped rather than disappearing. Eager enumeration remains the default, and the
  lifecycle difference is stated rather than papered over: an eager source is read before any hook
  runs, a deferred one at the start of the run. At 10,000 rows the benchmark records deferred
  discovery at 1.3 KB in 18us against eager's 9,407.88 KB in 5.767ms, with the fan-out cost moving to
  execution rather than multiplying.
- `NextUnit.Templates` packages a `dotnet new nextunit` project template, so a new test project starts
  from one command as it already does for MSTest, NUnit, and xUnit. The generated project targets
  Microsoft.Testing.Platform, references only the `NextUnit` meta-package, and carries one passing
  example test. The NextUnit version in the template content is a pinned literal, because a generated
  project lives outside this repository and never sees `Directory.Packages.props`; CI compares that
  literal against `Directory.Build.props` and fails the build when the two diverge.
- Selective retry. `[Retry<TPolicy>(count)]` attaches an `IRetryPolicy` that is asked, after each
  failed attempt that has a further attempt available, whether to run the test again. The decision is
  asynchronous and receives the exception, the attempt's `ITestContext`, the one-based attempt number,
  the total budget, and the run cancellation token. `[Retry(count)]` is unchanged and keeps retrying
  every non-timeout, non-skip, non-cancellation failure, so no existing test behaves differently. The
  generator emits a direct constructor call for the policy, which keeps the retry path free of
  reflection under Native AOT, and the `new()` constraint makes the compiler reject a policy without a
  public parameterless constructor.
- `ITestContext.RetryAttempt` exposes the one-based number of the attempt currently running, as a
  default interface member so an `ITestContext` implemented outside NextUnit still compiles. When a
  retried test ultimately fails, its reported output ends with the attempts it actually ran
  (`[NextUnit] Test failed after 2 of 5 attempts.`), which shows when a policy stopped the sequence
  early. There is no separate retry statistics store; the count travels with the ordinary test result.
- `NU0015` reports a method or class carrying both `[Retry]` and `[Retry<TPolicy>]`. The two are
  distinct types, so the compiler's own duplicate-attribute check does not catch the combination even
  though both declare the same attempt budget.
- `NU0016` reports a retry policy the generated registry cannot construct. A private or protected
  nested policy satisfies the `new()` constraint at the attribute and would otherwise fail the
  consumer's build with `CS0122` inside generated code.
- `NU0017` reports a retry count below 1 on either retry attribute. The attribute constructor rejects
  the value, but the generated path reads the attribute arguments without ever constructing the
  attribute, so a `[Retry(0)]` previously reached the engine and aborted the run with an internal
  error instead of failing the build.
- Deterministic culture control. `[Culture(name)]` and `[UICulture(name)]` pin the current culture and
  the UI culture a test runs under, and `[InvariantCulture]` is shorthand for pinning both to the
  invariant culture. All three apply at assembly, class, and method level. The two axes resolve
  independently and the most specific declaration wins, so a method can override the culture while
  inheriting the UI culture; `[InvariantCulture]` supplies only the axes its level leaves unspecified,
  which makes combining it with an explicit `[UICulture]` a composition rather than a conflict. The
  culture covers the whole attempt -- constructor, test-scoped hooks, test method, and disposal -- and
  is reapplied per `[Retry]` attempt, so an attempt that changes it cannot decide what the next one
  starts from. Because it is set inside the test's own asynchronous flow, tests running in parallel
  never observe each other's culture, and the previous culture is put back after a pass, a failure, a
  timeout, and a cancellation alike. Names are emitted as literals and resolved with
  `CultureInfo.GetCultureInfo`, so nothing on the path reflects and it stays usable under Native AOT.
  A name matching no culture on the executing machine fails that test with a message naming it,
  instead of ending the run. Display names are built during discovery, so a declared culture does not
  change them.
- `NU0018` reports a malformed culture name on `[Culture]` or `[UICulture]`. Only names no machine
  could accept are rejected at build time -- whether a well-formed name matches an installed culture
  depends on the machine running the tests, not the one running the build.

### Technical Notes

- Gate performance regressions on a repeated, noise-aware decision backed by a rolling history on the
  `benchmark-data` branch. Runs are compared on per-round ratios against the competing frameworks
  measured on the same machine in the same round, so hosted-runner speed cancels out of the metric. A
  finding must be at least 5% slower, exceed three robust standard deviations of the observed
  run-to-run spread, and clear a one-sided Mann-Whitney U test at p < 0.01 before it is flagged, and
  failing additionally requires the preceding recorded run to have regressed as well. Only
  default-branch runs append to the history, so a pull request reports into the job summary and a
  comment rather than failing on its own noise.
- Block pull requests that introduce vulnerable packages. The Security Scan job now diffs the head
  against the base revision and fails only on vulnerabilities the base does not already resolve, so an
  advisory landing on a package the repository already resolves no longer breaks unrelated work; the
  nightly job runs the same scan without a baseline and owns the existing findings. `NuGetAuditMode`
  is `all` and audit findings are warnings, so restore no longer fails for everyone. Exceptions live
  in an allowlist scoped to a single package per entry, each with a reason and an expiry capped at 90
  days.
- Add NUnit and MSTest migration guides covering project setup, lifecycle, data sources, filtering,
  assertions, parallelism, retry, culture, context, and the features NextUnit deliberately does not
  replicate. The NextUnit samples in both guides -- every C# block that carries no annotation -- are
  extracted and run through a `GeneratorDriver` in CI, so a published sample cannot drift from a
  compiled copy of itself. A block annotated with the source framework is the code being migrated
  away from and is excluded, and any other annotation fails the check, so a typo cannot quietly drop
  a sample from coverage.
- Update the Microsoft.Testing, Microsoft.CodeAnalysis, and Microsoft.OpenApi dependencies.

## [1.18.0] - 2026-07-26

### Added

- Track the generator's `NEXTUNIT` diagnostics through Roslyn analyzer release files. The shipped
  release history was reconstructed from the release tags by finding, for each rule, the first tag
  whose tree contains it, so `RS2008` is active instead of suppressed and a new rule cannot ship
  without a release entry.

### Changed

- Unify the `Assert` tolerance and precision family behind one comparison core. The `double`
  precision, `double` absolute tolerance, and `decimal` precision overloads of `Equal` and
  `NotEqual` each carried their own tolerance lookup and comparison, which is how their NaN
  handling drifted apart; the contract now lives in one place. The frozen semantics are unchanged:
  precision overloads compare non-finite values exactly, and every `double` comparison tests exact
  equality before the tolerance, so a NaN difference is never within tolerance.
- Compare through `EqualityComparer<T>.Default` in the generic `Assert.Equal<T>` and
  `Assert.NotEqual<T>` overloads, which also removes the boxing of value-type arguments. This is a
  narrow behavior change: `EqualityComparer<T>.Default` prefers `IEquatable<T>.Equals`, where the
  previous `object.Equals` call went to the `Equals(object)` override. A type that implements
  `IEquatable<T>` without overriding `Equals(object)` is therefore now compared by value rather
  than by reference. .NET requires those two to agree, so only types that break that contract are
  affected.
- Harden `TestExecutionEngine` for the long-lived reuse that `NextUnitFramework` already relies on.
  The assembly setup lock is no longer disposed at the end of a run, and assembly-scope run state
  is reset after teardown so each run gets a matched assembly setup and teardown pair instead of
  running setup once per engine and teardown once per run. Single-run behavior is unchanged.
- Read the reported framework and command-line extension versions from the assembly informational
  version instead of hardcoded strings. Both had drifted from the package version, reporting
  `1.2.0` and `1.6.2`, and now track the build automatically.
- Emit generated sources with LF line endings on every host. The registry previously used
  `Environment.NewLine` while the entry point inherited whatever bytes its source literal held, so
  a single compilation could emit two newline conventions depending on the build machine.

### Fixed

- Catch, classify, and report failures from session-scoped `[Before]` and `[After]` hooks, which
  were the only lifecycle scope whose exceptions escaped unwrapped into the
  Microsoft.Testing.Platform callback. A setup hook that requests a skip now skips every test in
  the session with its reason, any other setup failure is surfaced through the test session result,
  and teardown catches per hook so one failure no longer skips the remaining hooks. Cancellation is
  classified as it is in assembly teardown, so a hook's unrelated `OperationCanceledException` is
  reported as a failure rather than swallowed as run cancellation.
- Throw `InvalidOperationException` when `TestExecutionEngine.RunAsync` is called while another run
  on the same engine is still in flight. Assembly state is shared across the instance, so an
  overlapping run previously skipped setup and tore the assembly down twice. Sequential reuse and
  parallelism within a single run are unaffected.
- Clarify in the xUnit migration guide that both `Assert.InRange` and `Assert.NotInRange` bounds are
  inclusive, and that `NotInRange` therefore fails on a value equal to either bound. Behavior is
  unchanged and matches xUnit.

### Technical Notes

- Complete the four-part tech-debt refactor. The WebApi sample tests join the solution, giving
  `NextUnit.AspNetCore` real CI coverage; test-helper doubles and descriptor construction are
  consolidated behind a shared builder; and the 1459-line `Assert.cs` is split into partial files
  by assertion family with every body moved verbatim.
- Deduplicate the descriptor projection shared by the runtime data-source expanders, and make the
  generator's pipeline models equatable records over strings and primitives so incremental caching
  actually holds and no Roslyn symbol is rooted between runs.
- Restructure generator emission behind an indent-tracking writer, move the inline diagnostic
  descriptors into one table, share attribute-name and return-kind constants between the generator
  and the analyzers, and extract test-instance activation and run-cancellation classification out
  of the execution engine. Generator snapshots and package layout are byte-identical throughout.

## [1.17.0] - 2026-07-25

### Changed

- Report an assembly teardown failure as a synthetic `[AssemblyTeardown]` test result instead of
  throwing out of the run, so the failure is visible in Microsoft.Testing.Platform and VSTest
  results. A run whose only failure is assembly teardown no longer surfaces as a run-level
  exception.

### Deprecated

- Mark the `Assert.Throws<TException>(Action, string expectedMessage, string? message)` and
  `Assert.ThrowsAsync<TException>(Func<Task>, string expectedMessage, string? message)` overloads
  `[Obsolete]`. A two-argument call always binds to the custom-message overload, so the message
  validation is unreachable unless a third or a named argument is passed explicitly. Assert on the
  returned exception's `Message` instead. The overloads keep working in 1.x and are scheduled for
  removal in NextUnit 2.0. The deprecation is a warning (`CS0618`), not an error; projects that
  build with `TreatWarningsAsErrors` and cannot migrate yet can suppress `CS0618` at the call site
  until 2.0.

### Fixed

- Report a per-test instance whose `Dispose` or `DisposeAsync` throws as a failure of that test
  instead of letting the exception escape the run uncaught. When the test itself also failed, both
  exceptions are reported with the test failure first, and the test is not retried.
- Correct the `Assert.NotInRange` XML documentation, which described the range bounds as exclusive
  while the implementation has always treated them as inclusive. Behavior is unchanged.

## [1.16.0] - 2026-07-25

### Added

- Add typed `TestDataRow<T>` metadata for `[TestData]` and class data sources, including per-row
  display names, categories, tags, and skip reasons across Microsoft.Testing.Platform and VSTest.
- Diagnose statically knowable row-shape and metadata errors at build time.
- Support `ValueTask` and `ValueTask<T>` return types for test and lifecycle methods on both the
  generated and the reflection execution paths.
- Add `Assert.Same` and `Assert.NotSame` for reference identity; both reject value-type arguments,
  which boxing would otherwise make near-always unequal.
- Add `Assert.Fail` for unconditional failure.
- Add `Assert.DoesNotThrow` and `Assert.DoesNotThrowAsync`, which report the caught exception and
  let skip, cancellation, and critical fail-fast exceptions propagate unwrapped.
- Add absolute-tolerance `Assert.Equal` and `Assert.NotEqual` overloads for `double`, following
  xUnit NaN and infinity semantics. An `int` third argument still binds to the precision overload.
- Add `NU0011`, which reports unsupported test and lifecycle return types at build time instead of
  letting generated code fail to compile.
- Add `NU0013`, which warns when a method carries a data-source attribute but no `[Test]` and is
  therefore silently ignored by the generator.
- Extend `NU0001` to `[Before]` and `[After]` methods so async void lifecycle hooks are diagnosed.
- Add behavioral unit tests for the `Assert` API and runtime behavior tests for the execution
  engine.

### Changed

- Generate direct test invokers, constructor factories, and data-source accessors so generated
  execution avoids runtime reflection.
- Batch scheduler work without repeatedly copying the remaining tests, and allocate execution
  context and output storage only when needed.
- Cache analyzer and VSTest lookup data, and replace synthetic benchmarks with engine, scheduler,
  and complete sample-suite measurements up to 10,000 tests.
- Restrict `NU0009` to the conversions the runtime argument converter can actually perform, so
  user-defined and tuple conversions fail at build time instead of at run time.
- Cross-check `NextUnitVersion` in `Directory.Packages.props` against `Version` in
  `Directory.Build.props` in the release version gate.

### Fixed

- Await `ValueTask` and `ValueTask<T>` test bodies instead of discarding them, so an asynchronous
  test that fails after its first await no longer reports a false pass.
- Emit valid C# literals for `float` and `double` arguments that are NaN, infinity, or at the
  type's range limits, instead of generated code that fails to compile.
- Convert runtime data-source arguments with C#'s implicit numeric widening rules, including
  `nint` and `nuint`, instead of a hard cast that threw `InvalidCastException`.
- Give `[Timeout]` a per-attempt budget under `[Retry]` instead of one budget for the whole retry
  loop.
- Propagate `OperationCanceledException` for a cancelled run, and classify it by token provenance
  so an unrelated cancellation is reported as a failure rather than swallowed as run cancellation.
- Stop starting serial tests and stop retrying once the run is cancelled, and surface cancellation
  that first fires during cleanup instead of completing the run successfully.
- Report `[After(Class)]` and instance disposal failures through the result sink on distinct
  `[ClassTeardown]` and `[ClassDispose]` nodes, aggregating multiple hook failures per class.
- Run every class and assembly teardown hook to completion, and surface the collected cleanup
  failures instead of losing them, including when reporting to the sink itself fails.
- Fail the run with an explicit error when `--test-name-regex` or `NEXTUNIT_TEST_NAME_REGEX` is not
  a valid regular expression, instead of silently running every test.
- Route display-name formatter failures to stderr so they survive Release builds, and warn once per
  formatter type instead of once per expanded data row.
- Make `TestContext` artifact and state-bag mutation thread-safe.
- Guard the `Assert.Throws`, `Assert.ThrowsAsync`, `Assert.DoesNotThrow`, and
  `Assert.DoesNotThrowAsync` families against caller mistakes: a null delegate now throws
  `ArgumentNullException`, and an asynchronous delegate that returns a null `Task` throws
  `ArgumentException` instead of an opaque `NullReferenceException` reclassified as an assertion
  failure.

### Security

- Pass the pull request title and the release tag name into workflow shell steps through `env:`
  instead of inline expressions, closing a shell injection vector on the publish path.
- Pin the `NuGet/login` action to its resolved commit SHA instead of the mutable `v1` tag.

## [1.15.1] - 2026-07-22

### Fixed

- Package the Microsoft Testing Platform builder hook through the canonical
  `buildMultiTargeting` layout, with `build` and `buildTransitive` imports.
- Keep `NextUnit.Platform` as a normal runtime dependency instead of marking it
  as development-only.
- Register source-generated test metadata directly with the in-process host so
  Native AOT test applications can discover and execute trimmed test suites.
- Format complex-object assertion failures without reflection under Native AOT
  while preserving rich property output for normal JIT execution.
- Publish and verify all six packages, including the first NuGet.org publication
  of `NextUnit.AspNetCore`, from an explicitly selected and tested solution.

## [1.15.0] - 2026-01-25

### Added - ASP.NET Core Integration Package

- **`NextUnit.AspNetCore` NuGet package** - First-class ASP.NET Core integration testing support
  - Lightweight package with `Microsoft.AspNetCore.Mvc.Testing` dependency
  - Separate from meta-package (users explicitly opt-in)

- **`WebApplicationTest<TEntryPoint>` base class** - Simplified test fixture for web apps
  - Lazy initialization of `Factory` and `Client` properties
  - Virtual methods for customization:
    - `ConfigureWebHost(IWebHostBuilder)` - Configure web host
    - `ConfigureTestServices(IServiceCollection)` - Replace services with mocks
    - `ConfigureClient(HttpClient)` - Set default headers, base address
  - Service resolution helpers: `GetRequiredService<T>()`, `GetService<T>()`, `CreateScope()`
  - Proper `IDisposable`/`IAsyncDisposable` implementation

- **`TestWebApplicationFactory<TEntryPoint>`** - Enhanced factory with fluent API
  - `WithWebHostBuilder(Action<IWebHostBuilder>)` - Configure web host
  - `WithTestServices(Action<IServiceCollection>)` - Configure test services
  - Helper methods: `GetRequiredService<T>()`, `GetService<T>()`, `CreateScope()`, `CreateAsyncScope()`

- **`ServiceCollectionExtensions`** - Helper extensions for service mocking
  - `RemoveAll<TService>()` - Remove all registrations of a service
  - `Replace<TService, TImplementation>()` - Replace with implementation type
  - `Replace<TService>(instance)` - Replace with singleton instance
  - `Replace<TService>(factory, lifetime)` - Replace with factory function

- **Sample project** - `samples/WebApi.Sample.Tests` demonstrating:
  - Basic API testing with `HttpClient`
  - Service mocking with `ConfigureTestServices`
  - Service resolution from test fixture

### Technical Notes

- `[NotInParallel("WebApplicationFactory")]` must be applied to concrete test
  classes (source generator does not traverse base types)
- Lazy initialization pattern used because NextUnit's lifecycle attributes are not inherited from base classes
- Disposal pattern: `Dispose()` calls `DisposeAsyncCore().AsTask().GetAwaiter().GetResult()`

## [1.14.1] - 2026-01-25

### Fixed

- **Fixed:** Static Assembly/Session lifecycle methods now correctly apply globally to all tests in the assembly.

## [1.14.0] - 2026-01-25

### Added - Test Execution Priority

- **`[ExecutionPriority(int)]` attribute** - Control test execution order
  - Higher values run first (e.g., `[ExecutionPriority(100)]` runs before `[ExecutionPriority(10)]`)
  - Default priority is 0 when not specified
  - Works within the same dependency level (does not override `[DependsOn]`)
  - Can be applied at method or class level (class level sets default for all tests)

### Added - Roslyn Analyzers Phase 2

- **`NU0003`**: `TestData`/`ValuesFromMember` references a non-existent or non-static member (Error)
- **`NU0005`**: Lifecycle methods ([Before]/[After]) with unhandled throws (Info)
- **`NU0007`**: DependsOn references non-existent test method (Warning)
- **`NU0008`**: MatrixExclusion value count doesn't match Matrix parameter count (Error)

### Changed - CI/CD Infrastructure

- **Native AOT verification** - Nightly workflow now compiles and runs AOT binary
- **CI optimization** - Reduced workflow jobs, added NuGet caching, build artifact sharing

### Technical Notes

- `ExecutionPriorityAttribute` with `AttributeTargets.Method | AttributeTargets.Class`
- `Priority` property added to `TestCaseDescriptor` and generator models
- `ParallelScheduler.GroupIntoBatches` sorts by priority within dependency levels
- Analyzer diagnostics follow existing patterns with proper severity levels

## [1.13.0] - 2026-01-24

### Added - Explicit Tests

- **`[Explicit]` attribute** - Mark tests to exclude from default runs
  - Tests marked `[Explicit]` are skipped during normal test execution
  - `[Explicit("reason")]` with optional explanation
  - Run with `--explicit` CLI flag to include explicit tests
  - Environment variable `NEXTUNIT_INCLUDE_EXPLICIT=true` also supported

- **Class-level `[Explicit]`** - Mark entire test class as explicit
  - All tests in the class inherit the explicit status
  - Method-level `[Explicit]` takes precedence over class-level

- **VSTest adapter integration**
  - Explicit tests filtered by default when running all tests
  - Explicit tests can be run by selecting them specifically in Test Explorer
  - `Explicit` and `ExplicitReason` traits visible in Test Explorer

### Technical Notes

- `ExplicitAttribute` with `AttributeTargets.Method | AttributeTargets.Class`
- `IsExplicit` and `ExplicitReason` properties added to all descriptor classes
- Pre-expansion filtering during test execution (data providers not invoked for filtered explicit tests)
- Discovery still expands all tests for Test Explorer visibility
- `TestFilterConfiguration.IncludeExplicitTests` property for platform-level control

## [1.12.0] - 2026-01-24

### Added - Test Artifacts

- **`TestContext.AttachArtifact(path)` method** - Attach files to test results
  - Attach screenshots, logs, videos, or any file to test results
  - Files are displayed in Test Explorer
  - `AttachArtifact(filePath, description)` for simple attachment

- **`Artifact` class** - Full control over artifact metadata
  - `FilePath` - Path to the artifact file
  - `Description` - Optional description
  - `MimeType` - MIME type (auto-detected if not specified)

- **Automatic MIME type detection** - Common file types recognized
  - Text: `.txt`, `.log`, `.json`, `.xml`, `.html`
  - Images: `.png`, `.jpg`, `.gif`, `.bmp`, `.webp`, `.svg`
  - Video: `.mp4`, `.webm`
  - Other: `.pdf`, `.zip`

### Technical Notes

- `ITestContext.Artifacts` property for accessing attached artifacts
- `ITestContext.AttachArtifact()` methods for attaching files
- `ITestExecutionSink` updated to pass artifacts to test results
- VSTest adapter uses `AttachmentSet` for Test Explorer integration
- Microsoft.Testing.Platform uses `TestMetadataProperty` for artifact metadata

## [1.11.0] - 2026-01-24

### Added - Combined Data Sources

- **`[Values]` attribute** - Inline values per parameter
  - `[Values(1, 2, 3)]` on test parameters
  - Multiple parameters create Cartesian product
  - Example: 3 values × 2 values = 6 test cases automatically

- **`[ValuesFromMember]` attribute** - Values from static member
  - `[ValuesFromMember(nameof(GetValues))]` references static method, property, or field
  - `MemberType` property to specify different type: `[ValuesFromMember("Values", MemberType = typeof(DataProvider))]`
  - Member must return `IEnumerable`

- **`[ValuesFrom<T>]` attribute** - Values from class data source
  - `[ValuesFrom<MyDataSource>]` references enumerable class
  - `Shared` property for instance sharing: `[ValuesFrom<T>(Shared = SharedType.PerClass)]`
  - `Key` property for keyed sharing

- **Cartesian product support** - Mix data sources per parameter
  - Combine inline values, member values, and class data sources
  - Automatic Cartesian product at runtime
  - Example: `[Values(1, 2)]` + `[ValuesFrom<Browsers>]` = 2 × N test cases

### Technical Notes

- `CombinedDataSourceDescriptor` for runtime test case expansion
- `CombinedDataSourceExpander` with Cartesian product computation
- `ParameterDataSource` and `ParameterDataSourceKind` for parameter-level data sources
- Generator diagnostics: NEXTUNIT010 (conflicting data sources), NEXTUNIT011
  (incomplete parameter sources), NEXTUNIT012 (missing key)
- Shared instance caching similar to `ClassDataSourceExpander`

## [1.10.0] - 2026-01-24

### Added - Class Data Source

- **`[ClassDataSource<T>]` attribute** - Class-based test data source
  - Data source class implements `IEnumerable<object?[]>`
  - Type-safe alternative to `[TestData]` with member name strings
  - AOT-compatible implementation via source generators

- **Multi-type variants** - Combine data from multiple classes
  - `[ClassDataSource<T1, T2>]` through `[ClassDataSource<T1, T2, T3, T4>]`
  - Data from all source types is concatenated

- **`SharedType` enum** - Instance sharing control
  - `SharedType.None` (default) - New instance per test method
  - `SharedType.Keyed` - Shared by key value across tests with same key
  - `SharedType.PerClass` - Single instance within test class
  - `SharedType.PerAssembly` - Single instance across assembly
  - `SharedType.PerSession` - Single instance across entire test session

- **Shared instance management**
  - `Key` property for keyed sharing: `[ClassDataSource<T>(Shared = SharedType.Keyed, Key = "db")]`
  - Proper disposal of `IDisposable`/`IAsyncDisposable` instances at cleanup

### Technical Notes

- `ClassDataSourceDescriptor` for runtime test case expansion
- `ClassDataSourceExpander` with `ConcurrentDictionary`-based instance caching
- Generator diagnostics: NEXTUNIT008 (conflicting data sources), NEXTUNIT009 (missing key for Keyed)
- `TypeofCompatibleFormat` added to avoid nullable type annotations in `typeof()` expressions

## [1.9.0] - 2026-01-22

### Added - Matrix Data Source & Roslyn Analyzers

- **`[Matrix]` attribute** - Cartesian product test data generation
  - `[Matrix(1, 2, 3)]` on parameters generates all combinations
  - Multiple parameters create full Cartesian product
  - Example: 3 values × 2 values = 6 test cases automatically

- **`[MatrixExclusion]` attribute** - Skip specific combinations
  - `[MatrixExclusion(1, "a")]` excludes that specific combination
  - Multiple exclusions supported
  - Useful for invalid or unnecessary test combinations

- **Roslyn Analyzers (Phase 1)** - Compile-time test validation
  - `NU0001` (Warning): Async void test methods should be async Task
  - `NU0002` (Error): Test methods must be public
  - `NU0004` (Error): Arguments count mismatch with method parameters
  - `NU0006` (Error): Timeout value must be positive

- **Code Fix Providers** - Automatic fixes for common issues
  - `NU0001` fix: Change `async void` to `async Task`
  - `NU0002` fix: Change method visibility to `public`

### Changed

- **Line endings normalized to LF** - Cross-platform consistency
  - `.editorconfig` now uses `end_of_line = lf`
  - Added `.gitattributes` for automatic line ending normalization
  - Fixes CI format check issues between Windows and Linux

### Technical Notes

- Analyzers target `netstandard2.0` for broad IDE compatibility
- Analyzers automatically included via `NextUnit.Core` package reference
- 22 analyzer tests covering all rules and code fixes

## [1.8.0] - 2026-01-22

### Added - Enhanced Parallel Control

- **Constraint keys for `[NotInParallel]`** - Fine-grained resource locking
  - `[NotInParallel("Database")]` - Tests sharing the same key run serially
  - `[NotInParallel("Database", "FileSystem")]` - Multiple keys supported
  - Tests with disjoint keys are grouped using union-find algorithm
  - Existing `[NotInParallel]` (no keys) continues to work as full serial execution

- **`[ParallelGroup("name")]` attribute** - Exclusive group execution
  - All tests in the same group run together, isolated from other groups
  - `[ParallelGroup("Integration")]` on class or method level
  - Groups respect `[ParallelLimit]` within the group

- **`ProceedOnFailure` property for `[DependsOn]`** - Continue despite dependency failures
  - `[DependsOn("Setup", ProceedOnFailure = true)]` - Run even if Setup fails
  - Default is `false` (skip dependent test if dependency fails)
  - Skipped tests are now properly reported and tracked

### Changed

- **ParallelScheduler** rewritten with constraint-based batching
  - Union-find algorithm for grouping tests by constraint keys
  - `ConcurrentDictionary` for thread-safe outcome tracking
  - Proper handling of dependency-skipped tests (dependents' counters decremented)

- **TestBatch** now includes:
  - `ConstraintKeys` - Keys that apply to this batch
  - `ParallelGroup` - Group name if applicable
  - `IsSkipBatch` - Indicates tests should be reported as skipped

### Technical Notes

- `DependencyInfo` class added to track per-dependency options
- `OutcomeTrackingSink` wrapper reports outcomes to scheduler
- Generator extracts constraint keys and parallel groups from attributes

## [1.7.1] - 2026-01-19

### Fixed

- **NuGet package reference issue** - Remove `DevelopmentDependency` setting that caused compile assets to be excluded
  - When updating NextUnit via NuGet Package Manager, `compile` assets were incorrectly excluded
  - This caused CS0616 (`[Test]` not recognized as attribute) and CS0103 (`Assert` not found) errors
  - Removed `DevelopmentDependency=true` from NextUnit.Core, NextUnit.TestAdapter, and NextUnit meta-package

## [1.7.0] - 2026-01-19

### Added - Display Name Customization

- **`[DisplayName("name")]` attribute** - Custom display name for tests
  - Override the default method name in Test Explorer
  - Supports placeholders `{0}`, `{1}`, etc. for parameterized tests
  - Example: `[DisplayName("Adding {0} + {1} should equal {2}")]`

- **`IDisplayNameFormatter` interface** - Custom formatting logic
  - Implement `Format(DisplayNameContext context)` method
  - Access method name, test class, arguments, and argument index
  - Example: Convert "UserLogin_ValidCredentials" to "user login valid credentials"

- **`[DisplayNameFormatter<T>]` attribute** - Apply custom formatter
  - Apply to method or class level
  - Class-level formatter applies to all tests in the class
  - Method-level `[DisplayName]` overrides class-level formatter
  - Also available as non-generic `[DisplayNameFormatter(typeof(T))]`

- **`DisplayNameContext` struct** - Formatting context
  - `MethodName` - Original method name
  - `TestClass` - Test class type
  - `Arguments` - Test arguments for parameterized tests
  - `ArgumentSetIndex` - Zero-based index for parameterized tests

## [1.6.9] - 2026-01-18

### Added - Test Context Injection

- **`ITestContext` interface** - Access runtime test information
  - `TestName`, `ClassName`, `AssemblyName`, `FullyQualifiedName`
  - `Categories` and `Tags` from attributes
  - `Arguments` for parameterized tests
  - `TimeoutMs` and `CancellationToken` for timeout control
  - `Output` for test output writer
  - `StateBag` for test-scoped data storage

- **`TestContext.Current` static property** - AsyncLocal access to current test context
  - Proper isolation in parallel test execution
  - Automatically set/cleared by test engine

- **Constructor injection** - Inject `ITestContext` alongside `ITestOutput`
  - Priority: `(ITestContext, ITestOutput)` > `(ITestContext)` > `(ITestOutput)` > `()`

### Added - Retry and Flaky Test Support

- **`[Retry(count)]` attribute** - Automatic retry on test failure
  - Retries up to `count` times on failure
  - Test passes if any retry succeeds
  - Class-level retry applies to all tests in the class
  - Method-level retry overrides class-level

- **`[Retry(count, delayMs)]`** - Retry with delay between attempts
  - `delayMs` specifies wait time between retries

- **`[Flaky]` attribute** - Mark tests as known to be flaky
  - `[Flaky]` - Mark without reason
  - `[Flaky("reason")]` - Mark with explanation
  - Informational attribute for documentation and filtering

- **Retry behavior**
  - Timeouts and runtime skips are not retried
  - Each retry gets a fresh test instance
  - Test output is captured per attempt

## [1.6.8] - 2026-01-18

### Added - Timeout Support

- **`[Timeout(milliseconds)]` attribute** - Set timeout for test methods
  - Method-level: `[Timeout(5000)]` sets 5 second timeout
  - Class-level: `[Timeout(3000)]` applies to all tests in the class
  - Method-level timeout overrides class-level timeout
  - Graceful cancellation via CancellationToken
- **`TestTimeoutException`** - Thrown when test exceeds timeout
  - Clear error message with timeout value
  - Reported as test error (not failure)
- **Timeout handling in TestExecutionEngine**
  - Uses linked CancellationTokenSource for clean cancellation
  - Distinguishes timeout from external cancellation
  - Timeout exceptions don't affect other tests

## [1.6.7] - 2026-01-18

### Added - Runtime Test Skipping

- **Assert.Skip(reason)** - Skip test during execution
- **Assert.SkipWhen(condition, reason)** - Skip when condition is true
- **Assert.SkipUnless(condition, reason)** - Skip when condition is false
- **Platform-specific skip helpers**:
  - `Assert.SkipOnWindows(reason?)`
  - `Assert.SkipOnLinux(reason?)`
  - `Assert.SkipOnMacOS(reason?)`
  - `Assert.SkipOnFreeBSD(reason?)`
- **TestSkippedException** - Exception type for runtime skip
- Skip reason displayed in VSTest and Microsoft.Testing.Platform

## [1.6.6] - 2026-01-14

### Fixed

- **NextUnit.targets causing build errors** - Removed OutputType=Exe setting
  - VSTest mode doesn't require executable output type
  - Removed GenerateProgramFile=false (not needed for VSTest)
  - Set IsTestProject=true for proper VSTest integration

## [1.6.5] - 2026-01-14

### Fixed

- **NextUnit.TestAdapter not included in solution file** - Added to NextUnit.slnx
  - v1.6.4 release workflow failed because TestAdapter wasn't in the solution
  - This caused the project to not be built in Release configuration

## [1.6.4] - 2026-01-14

### Fixed

- **NextUnit.TestAdapter package missing from NuGet** - Added TestAdapter to release workflow
  - v1.6.3 was released without NextUnit.TestAdapter package on NuGet
  - This version includes the TestAdapter package for VS Test Explorer integration

## [1.6.3] - 2026-01-14

### Added - Visual Studio Test Explorer Integration

- **VSTest Adapter** - New `NextUnit.TestAdapter` package for Visual Studio Test Explorer integration
  - `ITestDiscoverer` implementation for test discovery
  - `ITestExecutor` implementation for test execution
  - Full support for Visual Studio 2026 Test Explorer
  - Works with `dotnet test` command
  - Proper cancellation support for long-running tests

- **Simplified Test Project Configuration**
  - NextUnit meta-package now includes all required dependencies
  - No need for `Program.cs` entry point (VSTest mode)
  - Automatic Test Explorer integration with `IsTestProject=true`

### Changed

- **NextUnit meta-package** now includes:
  - `NextUnit.Core` - Core attributes and assertions
  - `NextUnit.Generator` - Source generator for test discovery
  - `NextUnit.TestAdapter` - VSTest adapter (NEW)
  - `Microsoft.NET.Test.Sdk` - VSTest integration

- **Sample projects** simplified to use meta-package references only

### Technical Notes

- VSTest adapter uses reflection to load `GeneratedTestRegistry` from test assemblies
- TestData expansion is optimized to only expand requested tests when filtering
- Exception handling improved with specific exception types for assembly loading
- `IsCriticalException` helper prevents swallowing critical runtime exceptions

### CI/CD Integration Documentation

- **CI/CD Integration Guide** - Comprehensive documentation at `docs/CI_CD_INTEGRATION.md`
  - TRX report format setup and usage (Visual Studio/Azure DevOps)
  - GitHub Actions integration with workflow examples
  - Azure DevOps pipeline configuration
  - Jenkins pipeline integration with xUnit plugin
  - GitLab CI configuration
  - General CI/CD best practices for NextUnit
  - Environment variable usage in CI systems
  - Troubleshooting common CI issues
  - Examples for test filtering, parallel execution, and timeout control

## [1.6.2] - 2025-12-20

### Added - CLI Improvements and Rich Failure Messages

- **Advanced test name filtering**
  - `--test-name` option with wildcard support (`*` and `?` wildcards)
    - Case-insensitive matching
    - Multiple patterns supported via multiple arguments
    - Environment variable: `NEXTUNIT_TEST_NAME`
  - `--test-name-regex` option for regular expression filtering
    - Full regular expression support
    - Case-insensitive by default
    - Compiled for performance
    - Multiple patterns supported
    - Environment variable: `NEXTUNIT_TEST_NAME_REGEX`
  - Filters work with OR logic and combine with existing category/tag filters
  - Added 11 comprehensive unit tests for filtering logic

- **Rich failure messages with visual diffs**
  - String comparison shows:
    - Character-level highlighting of first difference
    - Context display (±20 characters around mismatch)
    - Escaped control characters (`\n`, `\r`, `\t`, `\"`)
    - Full string display for short values (≤ 100 chars)
    - Length comparison
  - Collection comparison shows:
    - Item-by-item diff with index numbers
    - Clear indication of missing/extra items
    - Count comparison
    - First 10 differences displayed (with summary if more)
    - Full collection display for small sets (≤ 10 items)
  - Object comparison shows:
    - Type information
    - Property values (up to 5 properties)
    - JSON-like formatting for complex objects
    - Graceful handling of null values

### Changed

- Improved `Assert.Equal<T>` with type-aware rich formatting
- Created `AssertionMessageFormatter` utility class with type-specific formatters
- Extracted magic numbers to named constants for better maintainability
- Optimized collection equality checking to avoid duplicate enumeration
- Enhanced `TestFilterConfiguration` with test name filtering capabilities
- Updated `NextUnitCommandLineOptionsProvider` with new filtering options

### Documentation

- Added sample tests demonstrating rich failure messages in `RichFailureMessageTests.cs`
- Updated test coverage to 43 platform tests (all passing)
- Updated sample tests to 159 passing tests (8 intentionally skipped)

### Technical Notes

- Implements Priority 2.2 (Rich Failure Messages) from PLANS.md
- Implements Priority 2.3 Phase 1 (Advanced CLI Filtering) from PLANS.md
- Minimal performance impact - rich formatting only occurs on failures
- All formatting applied automatically based on type detection
- Maintains backward compatibility with existing assertions

## [1.6.1] - 2025-12-15

### Changed

- **Package Configuration** - Set `DevelopmentDependency=true` for all NextUnit packages
  - NextUnit.Core, NextUnit.Generator, NextUnit.Platform, and NextUnit meta-package
    now marked as development dependencies
  - Prevents transitive dependency propagation when consuming projects reference NextUnit
  - Improves package dependency management for library authors using NextUnit for testing

### Documentation

- Added `RELEASE_PROCESS.md` documenting the NuGet package release process
  - Step-by-step checklist for version updates
  - List of all files requiring version updates
  - Guidelines for Copilot agents to automate future releases

## [1.6.0] - 2025-12-14

### Added - Enhanced Assertions (Priority 1.1)

- **Approximate equality assertions for floating-point comparisons**
  - `Assert.Equal(double expected, double actual, int precision)` - Compare doubles with tolerance
  - `Assert.Equal(decimal expected, decimal actual, int precision)` - Compare decimals with tolerance
  - `Assert.NotEqual(double notExpected, double actual, int precision)` - Inverse with tolerance for doubles
  - `Assert.NotEqual(decimal notExpected, decimal actual, int precision)` - Inverse with tolerance for decimals
  - Handles special values (NaN, Infinity) correctly
  - Useful for scientific computing and financial applications

- **Collection comparison assertions**
  - `Assert.Equivalent<T>(IEnumerable<T> expected, IEnumerable<T> actual)` - Unordered collection equality
  - `Assert.Subset<T>(IEnumerable<T> subset, IEnumerable<T> superset)` - Verify subset relationship
  - `Assert.Disjoint<T>(IEnumerable<T> collection1, IEnumerable<T> collection2)` - No common elements
  - Enable more expressive tests for set operations and unordered collections

- **Enhanced exception assertions with message matching**
  - `Assert.Throws<TException>(Action action, string expectedMessage)` - Match exception message
  - `Assert.ThrowsAsync<TException>(Func<Task> action, string expectedMessage)` - Async variant with message
  - Provides more precise exception validation

- **Custom comparers support**
  - `Assert.Equal<T>(T expected, T actual, IEqualityComparer<T> comparer)` - Custom equality logic
  - Allows custom equality comparison for complex types
  - Useful for case-insensitive comparisons, custom business logic

### Changed

- Enhanced documentation in `docs/GETTING_STARTED.md` with new assertion methods
- Added comprehensive test suite in `samples/NextUnit.SampleTests/EnhancedAssertionTests.cs`
- 31 new test cases covering all enhanced assertion scenarios

### Notes

- Implements Priority 1.1 from PLANS.md: Enhanced Assertions and Diagnostics
- Maintains backward compatibility with all existing assertions
- Reduces boilerplate code and improves test readability
- Achieves better feature parity with xUnit and NUnit

## [1.5.0] - 2025-12-10

### Added - Predicate-based Collection Assertions

- **`Assert.Contains<T>(IEnumerable<T>, Predicate<T>)`**
  - Verifies that a collection contains an element matching a predicate
  - Returns the first matching element (not void) for chaining assertions
  - Enables xUnit-compatible syntax: `var match = Assert.Contains(items, item => item.Id == expectedId)`
  - Supports both lambda expressions and `Predicate<T>` delegates
  
- **`Assert.DoesNotContain<T>(IEnumerable<T>, Predicate<T>)`**
  - Verifies that a collection does not contain an element matching a predicate
  - Complements the predicate-based Contains for consistency
  - Useful for verifying absence of items with specific properties

- **`Assert.Single<T>(IEnumerable<T>, Predicate<T>)`**
  - Verifies that a collection contains exactly one element matching a predicate
  - Returns the single matching element for further assertions
  - Throws clear errors when zero or multiple elements match

### Changed

- Added comprehensive test coverage for new predicate-based assertions in `RichAssertionTests.cs`

### Notes

- These additions achieve complete xUnit API compatibility for collection assertions with predicates
- All existing tests continue to pass
- Migration from xUnit is now fully supported with correct return types and parameter types

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

### Documentation Changes

- **README.md** - Updated to state that tests should be executed using `dotnet run`
- **NUGET_README.md** - Removed `EnableMSTestRunner` from example project configuration
- **NextUnit.targets** - Removed conditional logic
  - Now unconditionally sets `OutputType=Exe` and `GenerateProgramFile=false`

## [1.3.1] - 2025-12-09

### Added - dotnet test Support Documentation

- **`Microsoft.Testing.Platform.MSBuild` package dependency**
  - Added as a direct package reference to NextUnit meta-package
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
- **NextUnit.csproj** - Added Microsoft.Testing.Platform.MSBuild
  as a package dependency to ensure proper restore
- **NextUnit.targets** - Simplified to only set OutputType=Exe
  (package dependency handles MSBuild integration)

### Fixed

- **Package restore issue** - Microsoft.Testing.Platform.MSBuild now properly restored
  - Was previously only referenced in build targets, now a proper dependency

### Note

- `dotnet run` remains the recommended way to run NextUnit tests
- `dotnet test` requires additional SDK configuration on .NET 10 and later
- See `docs/DOTNET_TEST_SUPPORT.md` for detailed setup instructions

## [1.3.0] - 2025-12-08

### Added - Test Output/Logging Integration

- **`ITestOutput` interface** - xUnit-style test output capability
  for writing diagnostic messages during test execution
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
  - Tests cover simple output, formatted output, multiline output, parameterized tests,
    async tests, and failed tests with output
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

- **`[TestData]` attribute support**
  - Source generator now processes `[TestData]` attributes for runtime test data expansion
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

### Performance

- **Test Discovery**: ~2ms for 86 tests
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
| ------- | ---- | ----- | -------- | ------ |
| 1.19.1 | 2026-08-10 | 1342 | --list-tests discovery reporting, keyword identifier escaping in generated type names | Released |
| 1.19.0 | 2026-08-05 | 1333 | Async and deferred [TestData] sources, selective retry with IRetryPolicy, deterministic culture isolation, dotnet new nextunit template | Released |
| 1.18.0 | 2026-07-26 | 1105 | Session hook failure reporting, engine reuse hardening, Assert tolerance unification, deterministic generator output | Released |
| 1.17.0 | 2026-07-25 | 956 | Assert.Throws expectedMessage overload deprecation, per-test disposal and assembly teardown failure reporting | Released |
| 1.16.0 | 2026-07-25 | 945 | Reflection-free generated execution, ValueTask support, Assert API additions, engine cancellation and teardown fixes | Released |
| 1.15.1 | 2026-07-22 | 683 | MTP package integration, Native AOT fixes, complete six-package release | Released |
| 1.15.0 | 2026-01-25 | 395+ | ASP.NET Core Integration | Released |
| 1.14.0 | 2026-01-25 | 380+ | ExecutionPriority, Roslyn Analyzers Phase 2 | Released |
| 1.13.0 | 2026-01-24 | 375+ | Explicit Tests | Released |
| 1.12.0 | 2026-01-24 | 365+ | Test Artifacts | Released |
| 1.11.0 | 2026-01-24 | 350+ | Combined Data Sources | Released |
| 1.10.0 | 2026-01-24 | 310+ | Class Data Source | Released |
| 1.9.0 | 2026-01-22 | 258+ | Matrix Data Source, Roslyn Analyzers | Released |
| 1.8.0 | 2026-01-22 | 236+ | Enhanced Parallel Control | Released |
| 1.7.1 | 2026-01-19 | 236+ | NuGet package fix | Released |
| 1.7.0 | 2026-01-19 | 236+ | Display Name Customization | Released |
| 1.6.3 | 2026-01-14 | 236+ | VSTest Adapter, VS Test Explorer | Released |
| 1.6.2 | 2025-12-20 | 167+ | CLI Filtering, Rich Failure Messages | Released |
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
