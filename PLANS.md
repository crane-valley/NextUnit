# NextUnit Development Plans

## Current state

**Current version**: 1.19.0 (stable)

**Last audited**: 2026-07-23 (Asia/Tokyo, UTC+09:00)

NextUnit is a .NET 10+ test framework built around compile-time discovery, generated delegates,
Microsoft.Testing.Platform, Native AOT, classic assertions, and a one-package installation path.

- Completed implementation history and retired candidates: [PLANS-archive.md](PLANS-archive.md)
- Release history: [CHANGELOG.md](CHANGELOG.md)
- Current benchmark methodology and competitor set: [docs/PERFORMANCE.md](docs/PERFORMANCE.md)

## Product direction and guardrails

- Prioritize reproducible runtime and Native AOT performance plus frictionless one-package adoption.
  Competitor feature parity is not a goal.
- Add a core feature only when it removes recurring test-code workarounds, improves deterministic
  execution or diagnosis, or materially lowers adoption friction.
- Preserve source-generated discovery, trimming and Native AOT compatibility. Every new public
  feature needs generator/analyzer coverage and both framework-dependent and AOT validation.
- Prefer interoperability with Microsoft.Testing.Platform and general-purpose .NET libraries over
  framework-owned integrations. Add an integration package only after concrete demand proves that
  lifecycle hooks, artifacts, and ordinary package composition are insufficient.
- Keep benchmark numbers in `docs/PERFORMANCE.md` and generated benchmark artifacts, not here.

## Active roadmap

### Priority 0 — Data-driven test usability

The compared frameworks expose richer dynamic data without requiring users to give up filtering,
diagnostics, or AOT. NextUnit already has inline, member, class, matrix, and combined data sources;
the missing work is metadata and scalable asynchronous enumeration.

#### Typed per-row metadata

- [x] Add a typed data-row representation usable by `TestData` and class data sources.
- [x] Support per-row display name, categories, tags, and skip reason without changing the test
  method signature.
- [x] Preserve row identity and metadata in Microsoft.Testing.Platform and the VSTest adapter so
  IDE selection and filtering behave consistently.
- [x] Diagnose incompatible row value types and invalid metadata at build time where the source is
  statically knowable.
- [x] Cover ordinary JIT, trimming, and Native AOT package-consumer paths.

#### Async and deferred data sources

- [x] Accept cancellation-aware `IAsyncEnumerable<T>` member data and task/value-task-wrapped
  member collections without runtime reflection in the AOT path.
- [x] Add explicit deferred enumeration for very large data sets so discovery can expose one
  placeholder and enumerate rows only during execution.
- [x] Document the selection/filtering tradeoff of deferred rows and keep eager enumeration as the
  default.
- [x] Benchmark discovery and execution with a 10,000-row source to prevent the feature from
  regressing startup or allocating an unbounded intermediate list.

### Priority 0 — One-command project creation

The .NET SDK ships MSTest, NUnit, and xUnit project templates, and TUnit publishes its own template.
NextUnit's package is now self-contained, but users still have to create and edit a project by hand.

- [x] Publish a small `NextUnit.Templates` package with one C# `dotnet new nextunit` project
  template.
- [x] Generate the minimal Microsoft.Testing.Platform project using only the `NextUnit` package and
  one passing example test.
- [x] Verify install, creation, restore, build, discovery, execution, and uninstall from a clean
  NuGet cache in CI.
- Guardrail: do not add separate ASP.NET Core, Playwright, or Aspire templates until repeated user demand
  shows that the base template plus normal package references is insufficient.

### Priority 1 — Selective retry and retry observability

The existing `[Retry]` retries every non-timeout, non-skip failure. TUnit and current NUnit allow
retry decisions based on the exception, while MSTest exposes the current run count.

- [x] Provide an extensible, async retry decision API with the exception, test context, and current
  attempt; keep today's retry-all behavior as the compatibility default. Shipped as `IRetryPolicy`
  plus `RetryContext`, attached with `[Retry<TPolicy>(count)]`. A policy that throws is reported
  alongside the test's own failure and stops further attempts, so an undecided policy is never read
  as either answer. `[Retry(count)]` keeps retrying every retriable failure and allocates no policy.
- [x] Expose the one-based retry attempt in `ITestContext` and include total attempts in the final
  failure output/result metadata. `ITestContext.RetryAttempt` is a default interface member, so an
  externally implemented context still compiles; the failure output of a retried test ends with the
  attempts actually run, which is what distinguishes an exhausted budget from a policy stop. Every
  failing end of a retry sequence carries it -- exhausted budget, policy stop, policy failure,
  timeout, and disposal failure -- while a pass or a runtime skip does not.
- [x] Prove cleanup, output, artifacts, cancellation, and `StateBag` semantics across attempts.
  Behavioral tests pin one instance and one set of test-scoped hooks per attempt, per-attempt output
  and artifacts with only the final attempt reported, a `StateBag` that starts empty every attempt,
  and the two cancellation classifications at the policy decision point.
- Guardrail: avoid a separate statistics store; reporting should flow through existing test results and
  Microsoft.Testing.Platform.

### Priority 1 — Deterministic culture isolation

- [x] Add assembly-, class-, and method-level culture control for current culture and UI culture,
  including an invariant-culture shorthand. Shipped as `[Culture(name)]`, `[UICulture(name)]`, and
  `[InvariantCulture]`. The two axes resolve independently, most specific first, so a method can
  override the culture while inheriting the UI culture; `[InvariantCulture]` supplies only the axes
  its own level leaves unspecified, which makes it compose with an explicit `[UICulture]` instead of
  conflicting with it. Names flow to the descriptors as literals and resolve through
  `CultureInfo.GetCultureInfo`, so the path stays reflection-free under Native AOT.
- [x] Restore the original culture after pass, failure, timeout, and cancellation, and prevent
  culture-changing tests from contaminating concurrently running tests. A per-attempt scope inside the
  retry loop applies the declared cultures and puts back what it captured, so every attempt starts
  from the declared culture rather than from whatever the previous attempt left. Concurrent isolation
  comes from `CultureInfo.CurrentCulture` being `AsyncLocal`-backed and the assignment happening
  inside the test's own flow; that is proven by a test where two overlapping tests hold different
  cultures across sixty suspension points, and which fails if the two never actually overlap.
  Measured while building it: the restore itself is not observable today, because the engine's own
  await points already discard the assignment; it is kept so the guarantee is structural rather than a
  side effect of which internals happen to suspend.
- [x] Add representative `en-US`, `ja-JP`, and invariant test runs for formatting, parsing, display
  names, and assertion messages. Covered by `TestExecutionEngineCultureTests`, which pins the ambient
  culture to a known baseline first so an assertion never compares a culture against itself on a
  machine that already defaults to it. Display names are covered by the decision rather than a run:
  they are built during discovery, outside any test's culture scope, so a declared culture does not
  change them and test identity stays stable.

### Priority 2 — Attribute metadata claims inheritance the generator does not implement

Surfaced by the Codex review of the culture isolation change (2026-08-04) against the new
`[Culture]`, `[UICulture]`, and `[InvariantCulture]` attributes, and then confirmed to apply equally
to every existing NextUnit attribute, so it pre-dates that change.

No NextUnit attribute specifies `Inherited` in its `[AttributeUsage]`, so all of them inherit the
default of `true` and advertise that a derived class or an overriding method picks them up. Nothing
implements that: `AttributeHelper` reads `ISymbol.GetAttributes()`, which returns only directly
applied attributes and never walks `INamedTypeSymbol.BaseType` or `IMethodSymbol.OverriddenMethod`.
A `[Timeout]`, `[Retry]`, `[Category]`, `[ExecutionPriority]`, or `[Culture]` on a base test class is
therefore silently ignored by everything derived from it.

The culture attributes deliberately match the existing behavior rather than diverging. Fixing only
them would make one family of attributes inherit while the rest do not, and marking only them
`Inherited = false` would make one family honest while the rest keep advertising something untrue --
either way the framework becomes harder to predict than it is today. The fix belongs at the level of
the shared lookup, once, for all of them.

- [ ] Decide between walking the base type and overridden method chain in `AttributeHelper` and
  declaring `Inherited = false` on the attributes, then apply the decision to every NextUnit
  attribute at once and cover a base test class and an overriding method.
- [ ] Include lifecycle methods in that decision. `RegistryEmitter.LifecycleMethodsFor` looks the
  hooks up by the test's exact `FullyQualifiedTypeName`, so a `[Before]` or `[After]` declared on a
  base test class never runs for the derived classes holding the tests. The failure is silent -- the
  tests still run, without their setup -- and both xUnit and MSTest run inherited hooks, so it is a
  migration hazard as well as a surprise. Surfaced by the Codex review of the migration guides
  (2026-08-04); the guides tell readers to declare hooks on each concrete class meanwhile.

### Priority 2 — Display names are formatted with whichever culture happens to be ambient

Surfaced while adding culture isolation (2026-08-04) and confirmed to pre-date it. Nothing in the
display-name path passes an `IFormatProvider`, so an argument that formats differently per culture
produces a different display name, and therefore a different reported test name, depending on the
machine rather than on the test.

Both ends have the problem, with different blast radii. At build time,
`DisplayNameFormatter.FormatPrimitiveForDisplay` falls through to `argument.Value?.ToString()`, so a
`[Arguments(1234.5)]` display name is baked as `1234,5` when the build machine is `de-DE` and
`1234.5` when it is `en-US`. At run time, `DisplayNameBuilder.FormatArgument` ends in
`arg.ToString()`, so a `[TestData]`, `[ClassDataSource<T>]`, or combined-source row is named using
the executing machine's ambient culture. `ArgumentFormatter` already got this right for the emitted
*literals* (`G17` with `InvariantCulture`); only the display strings were left.

The culture attributes do not reach either one, by design: display names are built during discovery,
outside any test's culture scope, so that test identity stays stable and filtering keeps working.
That is also why this was left alone here -- switching both paths to the invariant culture changes
existing display names, and therefore test IDs, for anyone building or running on a non-invariant
machine, which is a compatibility decision rather than a drive-by fix.

- [ ] Format display-name arguments with `CultureInfo.InvariantCulture` at both ends, and cover a
  double, a decimal, and a `DateTime` argument under a non-invariant ambient culture.

### Priority 1 — Performance regression detection

Weekly and pull-request round-robin comparisons already measure framework-dependent and Native AOT
executables and publish Markdown/JSON artifacts. The missing capability is a durable, noise-aware
decision rather than more schedules or report formats.

- [x] Store a rolling history with runner, SDK, runtime, framework versions, commit, and raw samples.
- [x] Compare like-for-like baselines and fail only on a repeated, statistically meaningful
  regression; never gate on one noisy median.
- Guardrail: keep the existing weekly schedule and path-filtered pull-request run. Do not add a second daily
  comparison workflow.

Delivered by `SpeedComparison.Analysis`, wired into `speed-comparison.yml`. The rolling history is a JSON
Lines file on an orphan `benchmark-data` branch, capped at the most recent 100 runs, because artifacts
expire and caches are evictable. Runs are compared on per-round ratios against the competing frameworks
measured on the same machine, which cancels hosted-runner speed out of the decision. Failing requires a
change that is at least 5% slower, exceeds three robust standard deviations of the observed run-to-run
spread, clears a one-sided Mann-Whitney U test at p &lt; 0.01, and repeats across two recorded runs. Only
default-branch runs append to the history, so a pull-request run can never reach the failing verdict; it
reports into the job summary and a comment instead. See `tools/speed-comparison/REGRESSION_GATE.md`.

### Priority 2 — Adoption documentation

- [x] Add concise NUnit-to-NextUnit and MSTest-to-NextUnit migration guides covering project setup,
  lifecycle, data sources, filtering, assertions, and deliberate non-equivalents.
- [x] Link the guides from the README and NuGet README and compile every code sample in CI.
- Guardrail: defer an automated Roslyn migration tool until issues or real migrations demonstrate repeated
  mechanical work that documentation cannot solve.

Delivered as `docs/MIGRATION_FROM_NUNIT.md` and `docs/MIGRATION_FROM_MSTEST.md`, guarded by
`tests/NextUnit.Docs.Tests`. That project extracts every fenced C# block from the guides and compiles
it through the NextUnit generator and analyzers, so the Markdown is the single source of truth and a
sample cannot drift from a compiled copy of itself. Blocks whose info string annotates the language
with a source framework name are the code being migrated away from and are excluded; any other
annotation, and any unrecognized fence language, fails the check, so a typo cannot silently drop a
sample. Running the generator and analyzers rather than only the compiler is what makes the check
meaningful for a guide that is mostly attributes: a data source without `[Test]`, a misnamed
`[TestData]` member, or an unreachable retry policy is reported here. The reference set is narrowed
to the shared framework plus the NextUnit package, so a sample cannot compile against something only
the test host has.

- [ ] Bring `docs/MIGRATION_FROM_XUNIT.md` under the same compile check. Its samples are bare method
  and statement fragments rather than compilation units, and two blocks are API listings with
  undeclared identifiers, so inclusion means rewriting the samples rather than annotating them.
- [ ] Reconcile `ParallelLimitAttribute` with what the generator reads. Its `[AttributeUsage]`
  declares `AttributeTargets.Assembly`, so `[assembly: ParallelLimit(4)]` compiles, but
  `NextUnitGenerator` resolves the limit from the test method and its containing type only and never
  from `ContainingAssembly`, so a suite-wide limit is silently dropped and the run falls back to the
  processor count. `[Timeout]` and the culture attributes already read the assembly, so the fix is
  either to do the same here or to drop the assembly target. Surfaced by the Codex review of the
  migration guides (2026-08-04); the guides document the current behavior meanwhile.
- [ ] Decide how documentation on `main` should present APIs that are not in the released package.
  Between releases, `README.md`, `docs/GETTING_STARTED.md`, and the migration guides describe what
  `main` implements while pinning the last published version, so a reader who installs the pinned
  version cannot compile those samples. The v1.19.0 cycle was the clear case: those documents
  described `[Retry<TPolicy>]`, the culture attributes, `DeferredEnumeration`, and
  `ITestContext.RetryAttempt` against a `Version="1.18.0"` pin for the whole cycle, and
  `PublicAPI.Unshipped.txt` listed all of them. The pin is correct by the Version Update
  Checklist, which bumps every document at release; the gap is that `main` documents `main` without
  saying so. Options are an unreleased marker on the affected sections, or accepting the gap and
  saying so once. This spans four documents and the release process, so it is not a per-guide fix.
- [x] Fix two claims in `docs/GETTING_STARTED.md` that the current code contradicts, both pre-dating
  this work: the skip section states that runtime conditional skipping is unsupported, while
  `Assert.Skip`, `Assert.SkipWhen`, and `Assert.SkipUnless` ship and are exercised by
  `samples/NextUnit.SampleTests/SkipTests.cs`; and the samples omit `using NextUnit;`, which compiles
  in `samples/NextUnit.SampleTests` only because that namespace nests under `NextUnit`.

### Priority 2 — Make dependency findings actionable

- [x] Replace the non-blocking vulnerability scan with a check that fails for a newly introduced
  known vulnerable direct or transitive package.
- [x] Support a narrow, reviewed, expiring allowlist for upstream vulnerabilities that cannot be
  removed immediately.
- Guardrail: keep Dependabot as the update mechanism; CodeQL and SBOM generation remain demand-triggered,
  not standing roadmap work.

Delivered as `.github/scripts/check-dependency-vulnerabilities.ps1`, driven from two jobs. The
`Security Scan` job on pull requests scans the head and the base revision with
`dotnet list package --vulnerable --include-transitive` and fails only on what the base revision
does not already resolve, so adding a vulnerable package fails while an advisory published against a
package `main` already carries does not. The nightly `Vulnerability Scan` job runs the same script
without a baseline and owns those existing findings. A finding is keyed by project, target
framework, package, and advisory, so pulling a package that a test project already carries into a
shipped project still counts as new; the resolved version is left out of the key so that a bump
between two versions sharing one advisory is not reported as a regression.
`.github/vulnerability-allowlist.txt` carries exceptions scoped to one advisory and one package,
with an expiry date at most 90 days out and a reason; an expired entry stops suppressing, warns on
pull requests, and fails the nightly, so it cannot rot and cannot block unrelated work. Both jobs
also fail when the tree holds a `.csproj` that none of the scanned targets reach, so a project
cannot escape the gate by staying out of the solution.

NuGet audit findings moved from errors to warnings in `Directory.Build.props` in the same change.
They were already blocking, but as restore errors across every project: on the day an advisory
landed on any resolved package, every build and every pull request broke, including work that
touched no dependency. The two jobs replace that with a failure aimed at whoever introduced the
package.

`actions/dependency-review-action` was the obvious candidate and does not fit this repository.
GitHub resolves transitive NuGet dependencies only through automatic dependency submission, which
runs on default branch pushes, so a pull request head is parsed statically from the project files.
Measured against this repository, a branch adding one package produced exactly one dependency graph
entry, `Serilog.AspNetCore >= 0`: no transitive packages, and no resolved version, because central
package management keeps versions in `Directory.Packages.props` rather than in the `.csproj`. A gate
built on that would report almost nothing while looking like it worked.

Three limits of the gate, surfaced by the Codex review of this change (2026-08-04) and left open
because each needs a decision outside it:

- [ ] Decide whether the `main` branch ruleset should require a review. A pull request supplies the
  workflow, the script, and the allowlist that judge it, so a crafted pull request can weaken its own
  `Security Scan` and still show it green. This is true of every check in the repository, not only
  this one, and the fix is a repository setting rather than a code change: require an approval, or
  require review of the last push.
- [ ] Decide whether required status checks should be strict. `strict_required_status_checks_policy`
  is off, so a pull request can merge on a `Security Scan` that ran before `main` moved. A vulnerable
  resolution that only appears once two branches combine, such as a central version pin meeting a new
  package reference, would not be seen until the nightly.
- [ ] Decide whether the pull request gate should also sweep configuration-dependent package
  references. `tools/speed-comparison/UnifiedTests` selects its test framework packages through
  `ItemGroup Condition="'$(TestFramework)' == ...`, so a default restore resolves none of them. The
  nightly scan covers all five values through the per-target property syntax; the gate does not,
  because five more restores on both sides of the diff would double its runtime for a benchmark
  harness that ships nothing.

### Priority 2 — The release checklist misdescribes how `Directory.Packages.props` carries the version

Surfaced by the Codex review of the `NextUnit.Templates` change (2026-08-04) and confirmed to
pre-date it. The Version Update Checklist tells the releaser to update six
`<PackageVersion ... Version="X.Y.Z" />` entries in `Directory.Packages.props`, and the
Troubleshooting section repeats the claim. All six entries actually read `$(NextUnitVersion)`, so the
one value to change is the `<NextUnitVersion>` property. Following the instruction literally replaces
six indirections with literals and leaves `NextUnitVersion` at the old value, which the release
workflow's tag-versus-version gate then rejects.

Left as-is by the template change because it was orthogonal to that package and touched instructions
the change did not otherwise alter.

- [x] Rewrite the `Directory.Packages.props` checklist item and its Troubleshooting counterpart around
  the single `<NextUnitVersion>` property.

Done in the 2026-08-05 documentation audit. The same audit found a second stale entry in the same
checklist: the `README.md` item named a `**Current Version**: X.Y.Z (Stable)` line that the file does
not contain, while omitting the `PackageReference` snippet the v1.19.0 release commit `de2034f`
actually bumped.

### Priority 2 — `--list-tests` reports no tests under Microsoft.Testing.Platform

Surfaced while verifying the `dotnet new nextunit` template (2026-08-04) and confirmed to pre-date it:
`tests/NextUnit.PackageSmoke`, which runs eight tests successfully, also reports
`0 tests found` for `--list-tests`. The template is not involved.

`NextUnitFramework.DiscoverAsync` publishes a `TestNodeUpdateMessage` for every discovered test, but
the node carries no `DiscoveredTestNodeStateProperty`. Microsoft.Testing.Platform counts only nodes
that carry it, so a discovery-only request reports nothing and exits with code 8 while an ordinary run
of the same assembly reports and passes every test. Anything that lists before running — a
`--list-tests` invocation, and any IDE or tool that discovers through the platform rather than the
VSTest adapter — sees an empty assembly.

Because of this, the `template-smoke` job proves discovery with `--minimum-expected-tests`, which
fails when fewer tests than expected are reported, rather than with `--list-tests`.

The 2026-08-05 documentation audit reproduced this on three more projects, so it is not specific to
the package smoke project or to consuming NextUnit as a package. `samples/Console.Sample.Tests` and
`samples/ClassLibrary.Sample.Tests` each report `0 tests found` for `--list-tests` while an ordinary
run reports 25 and 44 passing tests respectively, and `samples/NextUnit.SampleTests` reports the same
`0 tests found` while running its full suite. The audit reached the missing
`DiscoveredTestNodeStateProperty` independently, which corroborates the diagnosis above. This also
puts a caveat under `docs/GETTING_STARTED.md`, whose deferred-data-source section describes what
`--list-tests` reports for a deferred source; that description is correct about the intended
behavior and unobservable until this is fixed.

- [x] Attach `DiscoveredTestNodeStateProperty` to the nodes published by `DiscoverAsync` and cover
  `--list-tests` for a project that consumes NextUnit as a package.

Done. `DiscoverAsync` now publishes each node with `DiscoveredTestNodeStateProperty.CachedInstance`
as its first property; the run path is untouched. Measured against the published 1.19.0 package,
`tests/NextUnit.PackageSmoke` reported `0 tests found` for `--list-tests` with exit code 8; against
the locally packed fix it reports seven, and the `package-smoke` job now asserts that count.
`samples/Console.Sample.Tests` and `samples/ClassLibrary.Sample.Tests` list 25 and 44, matching what
an ordinary run reports, and `samples/NextUnit.SampleTests` lists its full suite. Discovery reports
seven for the package smoke project where a run reports eight because its deferred source stays one
placeholder until the run expands it, which is the documented behavior. The `template-smoke` job
keeps `--minimum-expected-tests` and adds a `--list-tests` assertion beside it, so the workaround is
no longer the only proof of discovery. The `docs/GETTING_STARTED.md` deferred-source description was
checked against the now-observable output and needs no change: `--list-tests` reports
`Add_FromDeferredSource (deferred data source: WideAdditionRows)`, one entry per source and no row
count, exactly as written.

### Priority 2 — Assert API defects found in the 2026-07-24 review

- [x] Assert API gaps found dogfooding 1.15.1 in an NUnit migration: added Same/NotSame, Fail,
  DoesNotThrow/DoesNotThrowAsync, and tolerance-based Equal/NotEqual double overloads (additive
  only; int third argument still binds to the precision overload, proven by test).
- [x] ThrowsAsync overloads shared the unguarded `await action().ConfigureAwait(false)` null-task
  flaw fixed in DoesNotThrowAsync (PR #165 review); the whole Throws family now rejects a null
  delegate with `ArgumentNullException` and a null returned task with `ArgumentException`, and
  DoesNotThrowAsync reports the null task the same way instead of as an assertion failure.

Found while adding behavioral tests for the Assert API; both change the public surface, so they
need a deliberate compatibility decision rather than a drive-by fix.

- [x] `Assert.Throws<T>(action, string)` and the async equivalent always bind to the
  custom-message overload, so the expectedMessage validation overload is unreachable with two
  arguments; resolved by marking the sync and async expectedMessage-validation overloads
  `[Obsolete]` in 1.x, with removal planned for 2.0 so the published API stays compatible.
- [x] `Assert.NotInRange` XML doc says the range is exclusive, but the implementation treats the
  bounds as inclusive; resolved by aligning the doc (and the getting-started reference) with the
  existing inclusive-bounds behavior, with no runtime change.
- [x] `Assert.Equal<T>`/`NotEqual<T>` and the exact double overloads compare via the static
  `object.Equals(object, object)`, which boxes value-type arguments on every assertion. Switching
  to `EqualityComparer<T>.Default.Equals` (and `expected.Equals(actual)` for the double overloads)
  preserves semantics, including NaN equality, while removing the allocations. Flagged by review
  on PR #176; deferred there because the Assert file split had to stay behavior- and
  byte-preserving. Resolved together with the tolerance-family unification, which edits the same
  bodies; characterization tests pin the per-overload NaN, infinity, signed-zero and boundary
  semantics plus the exact failure message texts. One narrow semantic consequence is intended and
  tested: `EqualityComparer<T>.Default` prefers `IEquatable<T>.Equals`, so a type that implements
  `IEquatable<T>` without overriding `object.Equals` now compares by value instead of by
  reference. .NET requires those two to agree, and every other assertion library resolves it the
  same way.

### Priority 2 — Engine follow-ups from the 2026-07-24 cancellation review

Pre-existing behaviors surfaced while hardening cancellation and teardown reporting; both need a
deliberate design decision rather than a drive-by fix.

- [x] A per-test instance whose `Dispose` throws propagates the exception out of `RunAsync`
  uncaught; decide whether to report it as a test-scoped error like class-level disposal.
  Resolved: the disposal failure is reported on the test's own node (the instance belongs to that
  test), combined after the test's own failure so it never masks it, and terminal so retry cannot
  discard it.
- [x] Assembly-teardown failures surface by throwing from `RunAsync` instead of a dedicated sink
  node; decide whether an assembly-scope synthetic node is worth the adapter-visible change.
  Resolved: added an `[AssemblyTeardown]` synthetic node mirroring the class-scope nodes, so the
  failure is a test result in both adapters instead of an exception thrown out of `RunAsync`.

### Decided — data sources must not block synchronously

Decided 2026-08-03 while adding async member data. Cancellation is honored at every genuine await
point in the expander, but a data source that blocks its calling thread cannot be interrupted by any
token: the enumerator race only helps once `MoveNextAsync` has returned a pending task, and a
`MoveNext` that blocks never gets that far. The contract is now stated on `TestDataAttribute` and in
`docs/GETTING_STARTED.md`.

Running enumeration on a pool thread would close the gap and was rejected:

- It changes the observable threading contract for every data source, synchronous ones included:
  thread affinity, current culture, and any ambient context a source reads today would move.
- An abandoned enumeration leaks its thread for the process lifetime, trading a stall the user can
  see and fix for a leak they cannot.
- The limitation is not new. A blocking synchronous `[TestData]` member has always stalled discovery;
  async sources inherit that property rather than adding it.

Revisit only if a concrete report shows a source that cannot avoid blocking, and treat it as its own
change with its own review rather than a follow-up to the async work.

### Priority 2 — Async data source follow-ups deferred by the PR #202 review

All three were raised in the final review round of the async member data work, replied to on the pull
request, and deliberately left out of it: each one changes observable behavior or needs a design
decision, and none of them was introduced by that change.

- [ ] Abandoned work in the async data source path goes unobserved. `TestDataExpander` walks away
  from a `MoveNextAsync` that lost its race against the cancellation token, and from the matching
  `DisposeAsync`, on purpose -- awaiting either would reintroduce the hang the race exists to
  prevent. Nothing observes those tasks afterwards, so a source that later faults raises
  `TaskScheduler.UnobservedTaskException` from a task nobody owns. `NextUnitFramework.StartBuild`
  already has the shape this needs: a fault-only continuation whose whole body reads
  `task.Exception`. Applying it here means deciding whether an abandoned source's failure should stay
  silent or be logged, which is a reporting decision rather than a drive-by fix.
- [ ] A cancellation-token-taking member returning a type that implements both `IEnumerable<T>` and
  `IAsyncEnumerable<T>` binds to nothing, with no diagnostic. `KnownDataSourceTypes.Classify` matches
  the synchronous interface first, deliberately, so that a type which meant `IEnumerable<T>` before
  async sources existed keeps meaning it. `DataSourceMemberResolver` then admits a token-taking
  method only when the classification is asynchronous, so this member falls out of both passes and
  resolves to nothing. `TestDataMemberAnalyzer` still finds a static member of that name, so `NU0003`
  does not fire; the generator emits no provider; and the runtime reflection fallback invokes the
  method with no arguments and reports a parameter-count failure that mentions neither the token nor
  the reason. Closing it means either widening the resolver or reporting the combination as a
  diagnostic, and that choice depends on which interface such a type is meant to be read through --
  which is exactly what the sync-first rule already decided for the parameterless case.
- [ ] Ambiguous row-type selection when a collection implements more than one `IEnumerable<T>`.
  `NU0009` validates row values against the first constructed interface it finds, so a source type
  implementing, say, both `IEnumerable<object[]>` and `IEnumerable<TestDataRow<T>>` is validated
  against whichever the symbol enumeration happens to return first. Pre-existing and not specific to
  async sources -- it affects synchronous `[TestData]` and `[ClassDataSource<T>]` the same way -- but
  the fix is a deliberate precedence rule, not a tie-break chosen at random.

### Priority 2 — Emitted type names do not escape keyword identifiers

Surfaced by the Codex review of the selective retry change (2026-08-04) and confirmed to pre-date it.
`AttributeHelper.FullyQualifiedTypeFormat` and `TypeofCompatibleFormat` both omit
`SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers`, so a test class, data source type, or
parameter type whose identifier is an escaped keyword (`public class @event`) is emitted as
`global::event`. The generated registry then fails to parse in the consumer's build, with the error
pointing at a file the user did not write. The retry change added a `ConstructorCallFormat` that does
escape, because its emission was new; the pre-existing formats were left alone because changing them
rewrites every emission path and every snapshot baseline at once.

- [ ] Escape keyword identifiers in the shared emission formats, and cover a keyword-named test class,
  data source type, and parameter type.

### Priority 2 — Data source member lookup is narrower than C# member access

Both items were surfaced by the async data source review (2026-08-03) and verified against `main` to
pre-date it. Async sources inherit each behavior unchanged rather than widening it.

Non-public members break the generated registry. The generator emits direct member access from
`NextUnit.Generated.GeneratedTestRegistry`, so a `[TestData]` member that is `private` or `protected`
produces `CS0122` in the consumer's build. `TestDataMemberAnalyzer` accepts such members because the
runtime reflection fallback uses `BindingFlags.NonPublic`, so the analyzer and the generator disagree
about what is valid.

Inherited members are not found at all. Member lookup uses `INamedTypeSymbol.GetMembers`, which does
not walk the base type chain, so a `[TestData]` member declared on a base test class is reported as
`NU0003` even though C# resolves `Derived.Rows` fine. The runtime reflection fallback misses it the
same way, because `Type.GetMethod` does not return inherited statics without `FlattenHierarchy`.

- [ ] Decide between emitting an accessibility-safe accessor and reporting non-public data source
  members as a diagnostic, then align `TestDataMemberAnalyzer` with the decision.
- [ ] Walk the base type chain during member lookup, preserving the parameterless-overload precedence
  the generator and the analyzer now share.
- [ ] Cover `private`, `protected`, `internal`, and inherited members on the synchronous and
  asynchronous paths once the decisions are made.

### Priority 2 — Lifecycle follow-ups deferred by the 2026-07-26 refactor review

These items change observable behavior, so they were excluded from the refactor that surfaced them
and need a deliberate decision before implementation.

- [x] Give session-scoped hooks the same `try`/`catch` treatment as assembly- and class-scoped
  hooks. `ExecuteSessionSetupAsync` and `ExecuteSessionTeardownAsync` in `NextUnitFramework` let a
  hook exception propagate out of the platform callback, whereas the other scopes catch, attribute,
  and report it. Closing the gap changes how a failing session hook is reported, so it needs a
  decision on the reported shape rather than a drive-by fix. Resolved: the hooks moved into a
  `SessionLifecycleRunner` that decides the reported shape per phase. A setup hook throwing
  `TestSkippedException` records a session skip reason and every test in the session is reported
  skipped with it, mirroring the assembly-scope skip. Any other setup failure is surfaced through
  `CreateTestSessionResult` (`IsSuccess=false` plus an error message) instead of escaping the
  callback. Teardown catches per hook so a failure no longer skips the remaining hooks, aggregates
  multiple failures into an `AggregateException`, and surfaces them through
  `CloseTestSessionResult`; there is no per-test sink at session close, so the result object is the
  reporting channel rather than a synthetic node. Both phases classify
  `OperationCanceledException` with `RunCancellationClassifier`: genuine run cancellation propagates
  as the exception the platform expects (in teardown, only after the remaining hooks have run), while
  a hook's own unrelated cancellation is wrapped and reported as a failure.
- [x] Decide whether `TestExecutionEngine` should support overlapping `RunAsync` calls on one
  instance. Sequential reuse now works and pairs assembly setup with teardown per run, but assembly
  state (`_assemblySetupExecuted`, `_assemblySkipReason`) is shared across the instance, so a run
  starting while another is still executing would skip setup and then tear the assembly down twice.
  Documented as a caller constraint on `RunAsync`. Closing it means either serializing runs per
  engine or making assembly state run-local, both of which change the execution model, and no
  evidence yet shows Microsoft.Testing.Platform issuing overlapping run requests. Resolved: the
  sequential-only contract is affirmed and is now enforced rather than merely documented. `RunAsync`
  claims a non-reentrancy flag before it touches its arguments and throws `InvalidOperationException`
  on an overlapping call, releasing the claim once the run and its cleanup finish. Overlapping-run
  support is declined for now; parallelism within a single run is unaffected.
- [x] Decide whether the VSTest adapter should run session-scoped `[Before]`/`[After]` hooks.
  `NextUnitTestExecutor` reads only the assembly-scoped method arrays, so session hooks never run
  under VSTest; they do run under Microsoft.Testing.Platform. VSTest executes per assembly and has
  no session boundary, so wiring requires first defining whether the hooks mean once-per-session or
  once-per-assembly. Documented as a limitation on the executor until a concrete need settles it.
  Resolved: session hooks stay Microsoft.Testing.Platform-only. VSTest executes per assembly with no
  session boundary to attach them to, so wiring stays declined and the documented limitation on the
  executor stands; revisit only if a concrete need arises. No code change.
- [ ] Decide what session scope means if one framework instance ever serves two sequential sessions.
  `SessionLifecycleRunner` gates setup with an `AsyncOnceGate` that is never reset, while teardown is
  ungated and runs on every `CloseTestSessionAsync`, so a second session on the same instance would
  run teardown without a matching setup and would inherit the first session's skip reason. This is
  unreachable today: `RegisterTestFramework` builds one `NextUnitFramework` per test application, so
  the instance lifetime is the session, and running `[Before(Session)]` again would contradict the
  scope name. Surfaced by review on PR #183, which kept the pre-existing once-per-instance semantics.
  Closing it means either resetting the gate at close (session hooks re-run) or gating teardown to
  pair with setup; both change observable hook behavior, so neither is a drive-by fix.

## Deferred to the next major version

Breaking changes that are agreed in principle but cannot ship in 1.x. The `PublicAPI.Shipped.txt`
baselines freeze the current surface until then.

- [ ] Unify the shared-instance caches behind `[ClassDataSource]` and `[ValuesFrom]` and wire
  disposal to session end. `ClassDataSourceExpander` and `CombinedDataSourceExpander` each keep
  their own `PerSession`/`PerAssembly`/`PerClass`/`Keyed` caches, so one data source type used
  through both attributes is instantiated twice, and nothing in the run lifecycle ever clears them:
  the `ClearSharedInstances`/`ClearClassInstances` methods that would dispose the instances have no
  caller. Both the instance identity and the disposal timing are user-observable, and those methods
  are public API, so unification is a breaking change. Documented as-is on both expanders for 1.x.
- [ ] Demote the public types in the `NextUnit.Internal` namespace (`TestExecutionEngine`,
  the descriptors, the expanders, the delegates) to `internal`. The namespace name already signals
  the intent, but the types ship as public API today. Requires adding `NextUnit.Core.Tests` to
  `InternalsVisibleTo` and carving out the members that must stay public: `ArgumentConverter` (the
  generated user code calls it) and the expanders (the platform adapter reaches them).
- [ ] Remove the two `[Obsolete]` expectedMessage-validation overloads of `Assert.Throws` and
  `Assert.ThrowsAsync` (`Assert.Throws.cs`). They are unreachable with two arguments, which is why
  they were obsoleted in 1.x rather than deleted; their removal notice already promises NextUnit 2.0,
  so it is tracked here.

## Explicitly not planned

These items were considered during the 2026-07-23 audit and are intentionally absent from the
active queue:

- Framework-owned watch mode; use `dotnet watch`, IDE support, or platform tooling.
- Dedicated `MatrixSourceMethod` or `MatrixSourceRange`; `ValuesFromMember` already accepts static
  fields, properties, and methods, including methods that return numeric ranges.
- Aggregate repeat results; each repeat remains an individually diagnosable test case.
- First-party mocking, property-based testing, or snapshot testing libraries; use focused ecosystem
  packages unless an interoperability defect requires framework work.
- First-party Playwright, Aspire, Blazor, Minimal API, or gRPC packages/samples without concrete
  demand that existing lifecycle, artifact, and ASP.NET Core support cannot satisfy.
- A standalone documentation site, daily cross-framework benchmarks, a general CodeQL initiative,
  or SBOM generation without a scale, threat, or distribution requirement.
- A promise to reach a "complete feature set" relative to TUnit, xUnit, NUnit, or MSTest.

Reconsider a deferred item when an open issue or repeated user request identifies a real workflow,
when package-consumer validation exposes integration friction, or when benchmark evidence shows a
measurable performance or reliability cost.

## Completed summary

| Version | Shipped capability |
| ------- | ------------------ |
| 1.15.x | ASP.NET Core integration, reliable one-package Microsoft.Testing.Platform setup, Native AOT assertion/package validation |
| 1.14.x | Execution priority and analyzer phase 2 |
| 1.12.x–1.13.x | Artifacts, explicit tests, and major generator/runtime refactoring |
| 1.10.x–1.11.x | Class and combined data sources with shared instances |
| 1.6.x–1.8.x | Runtime skip, timeout, context, retry, repeat, display names, matrix data, analyzers, and parallel constraints |
| 1.0.x–1.5.x | Generated execution, lifecycle scopes, filtering, output, assertions, VSTest integration, and benchmark harness |

Full detail remains in [PLANS-archive.md](PLANS-archive.md) and [CHANGELOG.md](CHANGELOG.md).

## Audit sources

The competitor set comes from [docs/PERFORMANCE.md](docs/PERFORMANCE.md). The 2026-07-23 audit used
the official documentation for [TUnit data sources](https://tunit.dev/docs/writing-tests/method-data-source/),
[TUnit row metadata](https://tunit.dev/docs/writing-tests/test-data-row/),
[TUnit deferred enumeration](https://tunit.dev/docs/writing-tests/defer-enumeration/),
[TUnit retry](https://tunit.dev/docs/execution/retrying/),
[xUnit v3 features](https://xunit.net/docs/getting-started/v3/whats-new),
[NUnit attributes](https://docs.nunit.org/articles/nunit/writing-tests/attributes.html),
[MSTest test context](https://learn.microsoft.com/dotnet/core/testing/unit-testing-mstest-writing-tests-testcontext),
and the [.NET SDK project templates](https://learn.microsoft.com/dotnet/core/tools/dotnet-new-sdk-templates).
