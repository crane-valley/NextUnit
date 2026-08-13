# NextUnit Development Plans

## Current state

**Current version**: 2.0.0 (stable)

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

### Priority 2 — Attributes and lifecycle hooks are not inherited from base test classes

Surfaced by the Codex review of the culture isolation change (2026-08-04) against the new
`[Culture]`, `[UICulture]`, and `[InvariantCulture]` attributes, and then confirmed to apply equally
to every existing NextUnit attribute, so it pre-dates that change.

The generator reads directly applied attributes only: `AttributeHelper` goes through
`ISymbol.GetAttributes()` and never walks `INamedTypeSymbol.BaseType` or
`IMethodSymbol.OverriddenMethod`. A `[Timeout]`, `[Retry]`, `[Category]`, `[ExecutionPriority]`, or
`[Culture]` on a base test class is therefore silently ignored by everything derived from it. Most
NextUnit attributes used to leave `Inherited` unspecified, which defaults to `true`, and
`[Category]`, `[Tag]`, and both `[DisplayNameFormatter]` forms declared `Inherited = true` outright,
so the metadata advertised a behavior that nothing implemented.

- [x] Decide between walking the base type and overridden method chain in `AttributeHelper` and
  declaring `Inherited = false` on the attributes, then apply the decision to every NextUnit
  attribute at once. Decided in favor of the metadata: every NextUnit attribute now declares
  `Inherited = false`, so the declaration matches what the generator does. Walking the chains would
  change runtime behavior instead -- a `[Timeout]` or `[Retry]` on a base class that silently never
  applied would start applying -- which belongs in a major version rather than a patch, and fixing
  only the culture family would leave one family honest while the rest keep advertising something
  untrue. Nothing in NextUnit reads `Inherited`, at build time or at run time, so no generated code
  changes; what changes is that third-party tooling and readers are no longer misled.
- [ ] Implement inheritance for lifecycle hooks, and for attributes, in the next major version.
  `RegistryEmitter.LifecycleMethodsFor` looks the hooks up by the test's exact
  `FullyQualifiedTypeName`, so a `[Before]` or `[After]` declared on a base test class never runs for
  the derived classes holding the tests. The failure is silent -- the tests still run, without their
  setup -- and both xUnit and MSTest run inherited hooks, so it is a standing migration hazard as
  well as a surprise. Surfaced by the Codex review of the migration guides (2026-08-04); the guides
  tell readers to declare hooks on each concrete class meanwhile. Deferred rather than dropped
  because turning it on changes what runs: hooks that silently never ran would start running, and
  tests that quietly skipped a base class's setup would begin executing it.

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
the executing machine's ambient culture. `ArgumentFormatter` had it right for the emitted `float`,
`double`, and `decimal` *literals* (`G17` with `InvariantCulture`) but not for the integral ones,
where the consequence is worse than a name: an `sv-SE` build agent emits `-5` with U+2212, which the
C# lexer does not read as a number, so the generated registry does not compile.

The culture attributes do not reach either one, by design: display names are built during discovery,
outside any test's culture scope, so that test identity stays stable and filtering keeps working.

- [x] Format display-name arguments with `CultureInfo.InvariantCulture` at both ends, and cover a
  double, a decimal, and a `DateTime` argument under a non-invariant ambient culture. Both ends now
  route every remaining `IFormattable` argument through the invariant culture, which also closed the
  integer case. Test IDs are unaffected: an ID is structural -- `Type.Method` plus an `[index]`
  suffix -- and is never derived from a formatted argument, so what changes on a machine whose
  ambient culture is not invariant is the human-readable name, and therefore which tests a
  name-based filter matches.
- [x] Emit the integral and enum argument *literals* invariantly as well, which the Codex review of
  the display-name change surfaced as the more serious half: those are C# source, not text, so an
  `sv-SE` build agent produced a registry that did not compile. Covered by compiling the generator's
  output under a non-invariant culture rather than by inspecting it, which is also how the emitted
  cast of a negative enum member turned out to be `(Direction)-1` -- a subtraction to the C# parser,
  and a `CS0075` on every build regardless of culture.

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

- [x] Bring `docs/MIGRATION_FROM_XUNIT.md` under the same compile check. Its samples were bare method
  and statement fragments rather than compilation units, and two blocks were API listings with
  undeclared identifiers, so inclusion meant rewriting the samples rather than annotating them.

Delivered by rewriting every sample in the guide into a compilation unit and registering the guide in
`tests/NextUnit.Docs.Tests` alongside the other two. The xUnit-side blocks carry the `xunit`
annotation so they stay out of the compilation, and fourteen NextUnit-side blocks now compile through
the generator and analyzers. The two API listings took different shapes because they are different
claims: the assertions that survive migration unchanged became a class of signature listings, one
method per group, so the compile check proves that `Assert` still carries them; the assertions with
no counterpart became a table, because there is nothing to compile when the point of the section is
that the calls do not exist. `[assembly: ParallelLimit(4)]` moved into prose as an inline code span,
since a fenced block compiles inside a generated namespace where an assembly-level attribute is
`CS1730`. Several content bugs surfaced in the rewrite and are fixed here: the class-scoped lifecycle
sample held its connection in an instance field that no test would ever observe; the suggested
rewrites for `Assert.IsType<T>` and `Assert.Collection` dropped the exact-type and element-count
checks those calls perform, and ignored that the xUnit originals return the typed value; and the
NextUnit `.csproj` omitted the `ImplicitUsings` and `Nullable` properties the other two guides set,
without which the samples do not build in a reader's project. The larger correction is that
"assertions that work exactly the same" was an unbounded claim the compile gate cannot check. It now
says what the gate does prove -- that the calls compile unchanged -- and a `Where the Behavior
Differs` table carries the ones that keep their shape and change their rule: exception matching is
by subtype here and exact in xUnit, the string assertions are ordinal here and culture-aware in
xUnit, `Assert.All` stops at the first failure instead of aggregating, and the two equality
assertions disagree with each other on collections. `Assert.Equal` compares a sequence element by
element as xUnit does, but `Assert.NotEqual` uses `EqualityComparer<T>.Default`, which is reference
equality for arrays and `List<T>`, so `Assert.NotEqual(new[] { 1, 2 }, new[] { 1, 2 })` fails in
xUnit and passes here -- a red test turned green with nothing for the compiler to report. Nesting
fails the opposite way, since `Assert.Equal` compares elements with `object.Equals` and therefore
compares an inner array or `List<T>` by reference. Unordered collections can fail that way too:
`Assert.Equal` walks a `HashSet<T>` or `Dictionary<TKey, TValue>` in enumeration order where xUnit
ignores order, so two that enumerate differently are reported as different while two that happen to
enumerate alike still pass. `Assert.Equivalent` is the replacement, except for a set whose own
comparer carries meaning, since it compares with `EqualityComparer<T>.Default` instead. Each of those
claims was measured by probing the built `NextUnit.Core` and `xunit.v3.assert` assemblies side by
side.

- [x] Reconcile `ParallelLimitAttribute` with what the generator reads. Its `[AttributeUsage]`
  declares `AttributeTargets.Assembly`, so `[assembly: ParallelLimit(4)]` compiled, but
  `NextUnitGenerator` resolved the limit from the test method and its containing type only and never
  from `ContainingAssembly`, so a suite-wide limit was silently dropped and the run fell back to the
  processor count. Surfaced by the Codex review of the migration guides (2026-08-04). Delivered as
  `AttributeHelper.GetParallelLimit(IMethodSymbol, INamedTypeSymbol)`, which resolves the method,
  then its class, then the assembly exactly as `GetTimeout` and the culture attributes do; the
  assembly target stays, because dropping it would fail builds that compile today. Pinned by
  `tests/NextUnit.Generator.Tests/ParallelLimitEmissionTests.cs`, and the NUnit and MSTest guides
  now document the resolution instead of the gap.
- [x] Decide how documentation on `main` should present APIs that are not in the released package.
  Between releases, `README.md`, `docs/GETTING_STARTED.md`, and the migration guides describe what
  `main` implements while pinning the last published version, so a reader who installs the pinned
  version cannot compile those samples. The v1.19.0 cycle was the clear case: those documents
  described `[Retry<TPolicy>]`, the culture attributes, `DeferredEnumeration`, and
  `ITestContext.RetryAttempt` against a `Version="1.18.0"` pin for the whole cycle, and
  `PublicAPI.Unshipped.txt` listed all of them. The pin is correct by the Version Update
  Checklist, which bumps every document at release; the gap is that `main` documents `main` without
  saying so. Options are an unreleased marker on the affected sections, or accepting the gap and
  saying so once. This spans five documents and the release process, so it is not a per-guide fix.
  Done 2026-08-12: the gap is accepted and stated once per document rather than marked per section.
  `README.md`, `docs/GETTING_STARTED.md`, and the three migration guides each carry one sentence near
  the top: the documentation on `main` describes NextUnit as it stands there while pinning the latest
  release, so between releases it can name an API the pinned version does not ship yet, and an
  earlier version is reachable through its git tag. `docs/RELEASE_PROCESS.md` records that the
  sentence exists, which is what lets a release PR bump the pinned literals in the Version Update
  Checklist and stop rather than reread five documents for prose the release does not cover. The
  unreleased marker was rejected because it has to be placed and removed by hand, once per affected
  section, in sections nobody rereads at release time: it would go stale in exactly the way the
  checklist keeps the version literals from going stale. `NUGET_README.md` and
  `samples/ClassLibrary.Sample.Tests/README.md` pin a version too and are deliberately left out --
  the first is the package landing page, where a caveat about an unreleased `main` describes
  something the nuget.org reader cannot see, and the second documents a sample rather than the
  framework.
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

Three limits of the gate, surfaced by the Codex review of this change (2026-08-04). Each needed a
decision outside the change itself, and all three were decided on 2026-08-12:

- [x] Decide whether the `main` branch ruleset should require a review. A pull request supplies the
  workflow, the script, and the allowlist that judge it, so a crafted pull request can weaken its own
  `Security Scan` and still show it green. This is true of every check in the repository, not only
  this one, and the fix is a repository setting rather than a code change: require an approval, or
  require review of the last push. Declined: neither is implementable for a solo maintainer. GitHub
  does not accept an author's approval of their own pull request, so a nonzero
  `required_approving_review_count` blocks every merge, and the only way back out -- listing the
  author as a bypass actor -- hands the one person the rule is about an exemption from it. Requiring
  review of the last push has the same shape. The exposure stands and is accepted: the ruleset's
  `copilot_code_review` rule reviews every push and required review thread resolution keeps a finding
  from being merged past in silence, which is as close to a second reader as a one-person repository
  gets.
- [x] Decide whether required status checks should be strict. `strict_required_status_checks_policy`
  was off, so a pull request could merge on a `Security Scan` that ran before `main` moved. A vulnerable
  resolution that only appears once two branches combine, such as a central version pin meeting a new
  package reference, would not be seen until the nightly. Enabled on ruleset 10775711: a pull request
  now has to be up to date with `main` before it merges, so every required check runs against the
  combined tree rather than against a base that has moved. Of the three, this is the one a solo
  maintainer can turn on without a bypass that hollows it out.
- [x] Decide whether the pull request gate should also sweep configuration-dependent package
  references. `tools/speed-comparison/UnifiedTests` selects its test framework packages through
  `ItemGroup Condition="'$(TestFramework)' == ...`, so a default restore resolves none of them. The
  nightly scan covers all five values through the per-target property syntax; the gate does not,
  because five more restores on both sides of the diff would double its runtime for a benchmark
  harness that ships nothing. Declined: the gate keeps the default restore. The nightly already owns
  all five values, and the harness ships nothing, so the sweep would double the runtime of every pull
  request to shorten the reporting delay on a project no consumer restores.

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

- [x] Abandoned work in the async data source path goes unobserved. `TestDataExpander` walks away
  from a `MoveNextAsync` that lost its race against the cancellation token, and from the matching
  `DisposeAsync`, on purpose -- awaiting either would reintroduce the hang the race exists to
  prevent. Nothing observes those tasks afterwards, so a source that later faults raises
  `TaskScheduler.UnobservedTaskException` from a task nobody owns. `NextUnitFramework.StartBuild`
  already has the shape this needs: a fault-only continuation whose whole body reads
  `task.Exception`. Applying it here means deciding whether an abandoned source's failure should stay
  silent or be logged, which is a reporting decision rather than a drive-by fix. Done 2026-08-12:
  silent, matching `StartBuild`. Logging was rejected because the caller is already being told about
  the cancellation it asked for, and a second report naming work the run deliberately abandoned
  describes a failure nobody can act on -- the source was told to stop, and stopping mid-operation
  is how it was told. The continuation is attached whenever the awaited task did not complete
  successfully, which also covers the narrow case of a move that faulted just as the race was lost,
  and the successful per-row path allocates nothing extra. Left untested: the only observable is
  `TaskScheduler.UnobservedTaskException`, which fires from a finalizer, so a test would assert a
  negative behind a forced GC and depend on the abandoned task actually being collected --
  `StartBuild` ships the same shape untested for the same reason.
- [x] A cancellation-token-taking member returning a type that implements both `IEnumerable<T>` and
  `IAsyncEnumerable<T>` binds to nothing, with no diagnostic. `KnownDataSourceTypes.Classify` matches
  the synchronous interface first, deliberately, so that a type which meant `IEnumerable<T>` before
  async sources existed keeps meaning it. `DataSourceMemberResolver` then admits a token-taking
  method only when the classification is asynchronous, so this member falls out of both passes and
  resolves to nothing. `TestDataMemberAnalyzer` still finds a static member of that name, so `NU0003`
  does not fire; the generator emits no provider; and the runtime reflection fallback invokes the
  method with no arguments and reports a parameter-count failure that mentions neither the token nor
  the reason. Closing it means either widening the resolver or reporting the combination as a
  diagnostic, and that choice depends on which interface such a type is meant to be read through --
  which is exactly what the sync-first rule already decided for the parameterless case. Done
  2026-08-12: reported as `NU0021`, keeping the sync-first classification. Widening the resolver was
  rejected because it would answer the question the sync-first rule already answered, and answer it
  the other way for the token-taking overload alone. The resolver records the combination as a
  `DataSourceBindingIssue` so the generator withholds the provider it was already withholding and
  the analyzer names the fix -- drop the parameter, or return a type that is only
  `IAsyncEnumerable<T>`. A member returning a plainly synchronous collection with a token stayed
  unbound and unreported, on the grounds that the token was never meaningful there and that shape
  predates async sources. Superseded 2026-08-12 by PR #229: it now reports `NU0021` too. Leaving it
  silent meant a source that binds nothing and says nothing, failing instead at discovery with a
  member-not-found message, and `NU0021` -- a cancellation-aware member returning a synchronous
  collection -- describes the plain case as exactly as the mixed one. Reporting where the generator
  emits nothing is the invariant the analyzer now holds everywhere.
- [x] Ambiguous row-type selection when a collection implements more than one `IEnumerable<T>`.
  `NU0009` validates row values against the first constructed interface it finds, so a source type
  implementing, say, both `IEnumerable<object[]>` and `IEnumerable<TestDataRow<T>>` is validated
  against whichever the symbol enumeration happens to return first. Pre-existing and not specific to
  async sources -- it affects synchronous `[TestData]` and `[ClassDataSource<T>]` the same way -- but
  the fix is a deliberate precedence rule, not a tie-break chosen at random. Done 2026-08-12: one
  ordering rule, `KnownDataSourceTypes.SelectRowType`, applied to both element-type walks --
  `TestDataRow<T>` wins as the more specific contract, and remaining ties go to the ordinally first
  fully qualified element type name. Declaration order was rejected as the tie-break because
  `AllInterfaces` does not expose one. The non-generic `IEnumerable` walk needed no change: it
  answers a yes-or-no question, so the order it visits candidates in cannot affect the answer.
- [x] The selected row type does not reach the emitted provider. `TestDataSource` carries the shape
  but not the row type, so `BuildAsyncTestDataSourceProvider` emits a bare
  `AsyncDataSourceAdapter.FromAsyncEnumerableAsync(source, ct)`. A source implementing
  `IAsyncEnumerable<T>` more than once therefore fails the consumer's build with `CS0411`, because
  the type argument cannot be inferred, and a synchronous source read through the non-generic
  `IEnumerable` can yield rows of a different arm than the one `NU0009` validated. Pre-existing --
  the row type has never reached the generator -- and surfaced by the Codex review of the row-type
  precedence work (2026-08-12). Closing it means threading the selected row type through the
  descriptor model and emitting an explicitly typed adapter call, which moves every async snapshot
  baseline, so it is its own change with its own review. Done 2026-08-13: `TestDataSource` gained a
  `RowTypeName`, taken from the same `KnownDataSourceTypes.Classify` result its `Shape` comes from,
  and the `IAsyncEnumerable<T>` arm emits `FromAsyncEnumerableAsync<TRow>`. Recomputing the row type
  at the emitter was rejected because a second walk over the interfaces is a second precedence rule,
  free to drift from the one the analyzers apply. Naming the type argument of the task-wrapped arms
  was rejected too: theirs is the awaited collection rather than the row, and `Task<TRows>` admits
  exactly one inference, so it would move baselines to state what the compiler had no choice about.
  No baseline moved in the end, because the name is emitted only for a source offering more than one
  element type, which the classification now reports as `RowTypeIsAmbiguous`. Emitting it for every
  asynchronous source was rejected on the Codex review of this change: a written type name reaches
  nothing an `extern alias` hides, so naming an unambiguous row type would have failed the build of
  a source inference compiles today, and it settles nothing when there is only one candidate. The
  same limit leaves one case unfixed -- two same-named row types from two aliased assemblies, which
  `SelectRowType` tie-breaks on assembly identity and no written name can express -- and that case
  does not compile today either. The threading is orthogonal to the declaring-type item below --
  `RowTypeName` is its own field and touches neither `MemberTypeName` nor the emitted
  `DataSourceType` -- so that route is unaffected.
- [ ] Rows still reach the runtime through the non-generic `IEnumerable`, which is the other half of
  the bullet above. Both the synchronous provider and `AsyncDataSourceAdapter.FromTaskAsync`
  enumerate whatever `IEnumerable.GetEnumerator` dispatches to, so a source implementing
  `IEnumerable<T>` more than once still yields rows of a different arm than the one `NU0009`
  validated. Naming the row type at those call sites does not fix it: a cast selects no
  implementation, because the runtime re-reads the value as non-generic `IEnumerable` and dispatches
  virtually. Closing it means a typed synchronous adapter -- new public API on `NextUnit.Core`, and
  it moves every synchronous snapshot baseline rather than only the async ones, which is why it was
  left out of the row type threading. Nothing here fails a build: the shape resolves and runs, and
  only the arm chosen is wrong, which is why it is worth less than the `CS0411` half and is recorded
  separately rather than held open with it.

### Priority 2 — Emitted type names do not escape keyword identifiers

Surfaced by the Codex review of the selective retry change (2026-08-04) and confirmed to pre-date it.
`AttributeHelper.FullyQualifiedTypeFormat` and `TypeofCompatibleFormat` both omit
`SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers`, so a test class, data source type, or
parameter type whose identifier is an escaped keyword (`public class @event`) is emitted as
`global::event`. The generated registry then fails to parse in the consumer's build, with the error
pointing at a file the user did not write. The retry change added a `ConstructorCallFormat` that does
escape, because its emission was new; the pre-existing formats were left alone because changing them
rewrites every emission path and every snapshot baseline at once.

- [x] Escape keyword identifiers in the shared emission formats, and cover a keyword-named test class,
  data source type, and parameter type. Done 2026-08-07: both shared formats now escape, and
  `TypeofCompatibleFormat` and `ConstructorCallFormat` became byte-identical once they did, so they
  merged into one `TypeExpressionFormat`. No snapshot baseline moved -- escaping only changes output
  for identifiers that are keywords, and `UseSpecialTypes` still renders `int` as `int`. Test ids and
  display names keep the unescaped spelling, pinned by test: an id is a string literal matched by
  filter expressions, never parsed as C#.

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

- [x] Decide between emitting an accessibility-safe accessor and reporting non-public data source
  members as a diagnostic, then align `TestDataMemberAnalyzer` with the decision. Done 2026-08-12:
  reported as `NU0020`. An accessibility-safe accessor was rejected because reaching a `private`
  member without reflection means emitting something into the user's type, which the generator has
  never done and which no diagnostic could then explain. The rule is scoped to what actually breaks
  the build: the registry is emitted into the test assembly, so `internal` members and members of
  `internal` types are reachable and are not reported; `private`, `protected`, `private protected`,
  a member of a type nested in one of those, a file-local type, and another assembly's `internal`
  without `InternalsVisibleTo` are. `GeneratedRegistryAccess` decides it once for the resolver and
  the analyzer, and the resolver records the verdict so the generator withholds the direct access
  that used to fail with `CS0122`. The runtime reflection fallback still reads whatever reaches it.
- [x] Walk the base type chain during member lookup, preserving the parameterless-overload precedence
  the generator and the analyzer now share. Done 2026-08-12: `DataSourceMemberResolver` collects
  candidates across the chain most-derived first, and the analyzer's own existence test and the
  parameter-level reader take the same list, so a member one of them now binds can never be reported
  as missing by another. The walk applies C# hiding as it goes, because the generator emits
  `Derived.Rows` and that name has to mean here what it means to the compiler: a member that is not
  a method hides everything of that name below it, and a method hides a base member that is not one,
  so a base property is dropped once a derived type declares a method of the same name -- binding it
  would emit a property read where the compiler reads a method group, which does not compile.
  Methods accumulate across levels instead, and both resolver passes run over the whole flattened
  chain rather than per type, so a base `Rows()` still beats a derived `Rows(CancellationToken)` --
  the overload C# binds for a call supplying no arguments -- unless a nearer declaration repeats the
  signature, static or not, since a derived instance `Rows()` makes the emitted `Derived.Rows()` a
  `CS0120` in generated code and `NU0003` is the better answer. Interfaces are not walked: a static
  interface member cannot be named through an implementing type. A base type's `private` members are
  skipped, since C# member lookup never sees them from a derived type and they therefore neither
  bind nor hide; letting one win would report `NU0020` for a name that resolves further up the chain
  and compiles. A `private` member on the named type itself is still collected and refused, which is
  the existing rule and the reason an inherited `protected` source reports `NU0020` naming the fix
  rather than `NU0003` describing it as missing. The runtime reflection fallback moved into one
  `DataSourceMemberLookup` shared by `TestDataExpander` and `CombinedDataSourceExpander`, so
  `[TestData]` and `[ValuesFromMember]` cannot disagree about which member a name means. It selects
  over the flattened candidates by declaring type rather than by member kind: searching kind by kind
  reads a base property for a name a derived method has taken over, which runs a test against data
  the user never pointed at, and it also turns a flattened `Rows()` plus `Rows(CancellationToken)`
  into an ambiguous match. Instance members are candidates there for hiding only: one cannot be read
  as a data source, but it hides the base member of the same name, and a candidate list that leaves
  it out reads the member it hides. Nothing non-static is ever returned. The candidates are gathered
  with one call per member kind rather than a single `GetMember` with a `MemberTypes` mask, because
  the trimmer does not read that mask -- it treats `GetMember` as able to return any kind and demands
  the constructor, event, and nested-type annotations too, which is `IL2070` and a failed Native AOT
  smoke build.
- [x] Inherited member lookup is a closed contract, not a model of C# member lookup: **the nearest
  declaring level wins, or the source is diagnosed**. Whichever type first declares the name -- the
  type the attribute points at, or the closest base that declares it -- is the only type considered,
  and the existing single-type selection runs against it unchanged. If nothing on that level binds,
  for any reason at all, `NU0003`/`NU0020`/`NU0021` report it; resolution never falls through to a
  farther level. Decided 2026-08-12 after the PR #229 review.
  Modeling C# faithfully was tried first and abandoned. Three review rounds each found another slice
  of the specification the model got wrong -- cross-kind hiding, then applicability across levels
  (optional and `params` overloads), then accessibility staging across an assembly boundary, then
  implicit-conversion applicability -- and every wrong slice had the same shape: the resolver
  validating and classifying one member while the emitted call ran another, silently. The domain is
  unbounded, and reviewer and author disagreeing on specification minutiae is the signal that it
  cannot be closed by patching. The contract makes that failure structurally impossible instead: the
  emitted access names the nearest declaring level, so no nearer binding exists for the compiler to
  prefer.
  The accepted cost is that a base member becomes unreachable once any nearer type declares the same
  name, including cases C# resolves happily -- a derived `Rows(CancellationToken)` or `Rows(int)`
  over a base `Rows()` now reports rather than binding the base member. C#'s accumulation of method
  overloads across levels is explicitly not modeled. The fix a user makes is to declare the member on
  the derived type or rename one of them, which the diagnostic names. Loud and mechanical beats a
  silent mismatch between the rows validated and the rows run. When a farther type does declare the
  name, `NU0003` and `NU0020` append a sentence naming it and pointing at `MemberType`, which binds
  the base member directly: the contract is not guessable from a report that calls a member missing
  while the user is looking at a base class that declares it.
  The runtime reflection fallback follows the same contract, and scans a whole level before rejecting
  it so an overload it cannot invoke never hides a sibling it can. Its remaining blind spots are all
  the same shape: a level whose declaration reflection cannot see is walked past rather than stopping
  the search, where the compile-time walk stops there and diagnoses. That covers a level declaring
  only an event or a nested type -- this lookup does not ask for either kind, and `FlattenHierarchy`
  would not return an inherited nested type anyway -- and a base level whose only declaration is
  `private`, which `FlattenHierarchy` omits by design. All of them stay deferred: the compile-time
  walk does see every such declaration, so it stops there and the build fails before the fallback can
  run. Closing any of them costs the same thing: asking reflection for
  events and nested types needs those annotations on every entry point -- `GetMember` with a
  `MemberTypes` mask already demanded exactly that and broke the Native AOT smoke build with
  `IL2070` -- and seeing a base type's `private` members needs a `DeclaredOnly` walk that reflects
  over a `Type` obtained from `BaseType`, which carries no annotations at all. Reaching the fallback
  in any of these cases means having suppressed an error diagnostic first.
  `TestDataHiddenByDerivedEvent_ReportsNotFoundAsync`,
  `TestDataHiddenByNestedTypeOnIntermediateBase_ReportsNotFoundAsync`, and
  `TestDataWithPrivateMemberOnIntermediateBase_ReportsInaccessibleAsync` pin the guard.
- [x] The emitted data source access is qualified by the target type, so another source generator
  can capture it. Generators cannot see each other's output: a same-named member that a second
  generator adds to the same partial test class is invisible while NextUnit resolves, and present
  once every generated source is compiled together. `TestCaseEmitter` writes `Derived.Rows()`, so the
  final call can bind that foreign member while NextUnit classified and validated an inherited base
  one -- rows the user never pointed at, with no diagnostic, since the analyzer saw the same
  compilation the generator did. Raised by the Codex review of PR #229 and deferred there: the
  exposure needs a second generator targeting the same partial class and the same member name, and
  the fix is descriptor and emitter plumbing rather than a lookup change, which does not belong in a
  pull request already carrying a contract redesign.
  Done 2026-08-13 by the recorded route. `TestDataSource.DeclaringTypeName` and
  `ParameterDataSourceDescriptor.DeclaringTypeName` carry the resolved member's containing type from
  the same `DataSourceMemberResolver` result that decided the shape and the accessibility verdict,
  and the emitters qualify the provider access with it. A second lookup at the emitter was rejected
  for the reason the row type was: it would be a second precedence rule, free to drift from the one
  the analyzers validated.
  Both descriptors keep the target type as a separate field, exactly as the route required.
  `DataSourceType` and the parameter-level `MemberType` are unchanged, so the row id prefix the
  runtime builds from `DataSourceType.FullName` is unchanged: an inherited source keeps its
  `Derived.Rows` ids instead of moving to `Base.Rows`, and no snapshot baseline moved -- the id
  surface is pinned by an assertion on the emitted `DataSourceType` alongside the base-qualified
  access.
  The remaining exposure is one the compiler makes loud. Where the resolved member is declared on
  the target type itself, the emitted access still names that type, and a foreign generator adding
  the name there is a duplicate-member build error rather than a silent capture. Only the inherited
  case could ever bind silently, and that is the case this moved.
  `InheritedMember_IsNotCapturedByAConcurrentGeneratorAsync` and its parameter-level twin pin it by
  adding the foreign member to the compilation only after the generator has run, which is what a
  second generator's output actually looks like.
- [ ] A class data source type is not accessibility-checked. `[ClassDataSource<T>]` and
  `[ValuesFrom<T>]` emit `typeof(T)` and `new T()`, so an unreachable `T` fails the consumer's build
  with `CS0122` in a file the user did not write, with no diagnostic to explain it. The member paths
  no longer do this -- an unreachable `MemberType` is withheld from the descriptor and from the
  `DynamicDependency`, and `NU0020` names the fix -- but the same treatment cannot be applied here:
  the factory is the only way a class data source is constructed, so dropping the type would turn a
  build error into a source that silently supplies nothing. It needs a diagnostic instead, which is
  a new rule ID and its own change. `GeneratedRegistryAccess` already answers the question, so only
  the rule is missing. Surfaced by the Codex review of the accessibility work (2026-08-12).
- [x] Cover `private`, `protected`, `internal`, and inherited members on the synchronous and
  asynchronous paths once the decisions are made. Done 2026-08-12 for accessibility:
  `private`, `protected`, a public member of a `private` nested type, and `[ValuesFromMember]` are
  covered as `NU0020`, and `internal` is covered as the negative case in both the analyzer and the
  emission tests. Inherited members are still not found at all, so their coverage belongs to the
  base-chain bullet above and moves with it.

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
- [x] Decide what session scope means if one framework instance ever serves two sequential sessions.
  `SessionLifecycleRunner` gates setup with an `AsyncOnceGate` that is never reset, while teardown is
  ungated and runs on every `CloseTestSessionAsync`, so a second session on the same instance would
  run teardown without a matching setup and would inherit the first session's skip reason. This is
  unreachable today: `RegisterTestFramework` builds one `NextUnitFramework` per test application, so
  the instance lifetime is the session, and running `[Before(Session)]` again would contradict the
  scope name. Surfaced by review on PR #183, which kept the pre-existing once-per-instance semantics.
  Closing it means either resetting the gate at close (session hooks re-run) or gating teardown to
  pair with setup; both change observable hook behavior, so neither is a drive-by fix. Resolved: one
  runner serves one session, and that is now enforced rather than assumed. Opening a session, running
  session setup, and running session teardown all throw `InvalidOperationException` once teardown has
  claimed the instance, which is the same treatment `TestExecutionEngine.RunAsync` gives its
  sequential-only contract. The check runs before `CreateTestSessionAsync` builds its test cases,
  because a session whose filter matches no test returns before session setup and would otherwise be
  refused only at the close that follows it.
  Neither of the two options the item listed was taken. Resetting the gate at close invents a
  per-session re-run of `[Before(Session)]` that no caller asks for, and pairing teardown with setup
  would silently stop the `[After(Session)]` hooks on the reachable path where a filter matches no
  tests and `CreateTestSessionAsync` returns before setup runs. Enforcement was chosen over both
  because the instance-per-session contract is a fact of the host rather than a convention:
  Microsoft.Testing.Platform runs the registered framework factory inside
  `TestHostBuilder.BuildTestFrameworkAsync`, which `ConsoleTestHost` calls once per run and
  `ServerTestHost` calls per request, and `TestHostTestFrameworkInvoker` then issues exactly one
  create/execute/close cycle per instance (verified against microsoft/testfx `v4.3.3`, the tag
  carrying the platform 2.3.x sources this repo pins). A reused instance could not be served
  correctly in any case, because `NextUnitFramework` memoizes its test cases for the instance
  lifetime and session teardown disposes the session-shared data source instances those cases hold.
  Two interleavings are left unarbitrated on purpose: a second create before the session closes is
  answered by the setup that already ran, which is what once-per-session means, and a setup racing a
  teardown would need both phases under one lock held across user hooks, where a `[Before(Session)]`
  hook that never returns would hang session close instead of letting it release the shared instances.
  Pinned by five tests in `SessionLifecycleRunnerTests`.

### Priority 2 — A parallel group's declared limit overrides its unannotated members' default

Surfaced by the Codex review of PR #219 (2026-08-10) and confirmed to pre-date it.
`ParallelScheduler.GroupIntoBatches` sizes a `[ParallelGroup]` batch with
`groupTests.Min(t => t.Parallel.ParallelLimit) ?? _globalMaxDegreeOfParallelism`. `Min` over `int?`
skips nulls, so the coalesce is reached only when no member of the batch declares a limit at all. A
batch holding unannotated tests alongside one `[ParallelLimit(16)]` therefore runs sixteen wide,
including the unannotated tests, which everywhere else are bounded by the processor count -- on an
eight-core machine the batch runs at twice what those tests would otherwise get. The ungrouped path
does not share the defect: it keys on `ParallelLimit ?? _globalMaxDegreeOfParallelism`, so an
undeclared limit becomes the processor count before the grouping rather than after it. The unit is
the batch rather than the whole group, because `GroupIntoBatches` sees only the tests a dependency
wave has made ready: members of one group held back by dependencies land in a later batch and are
sized separately.

- [x] Decide whether an undeclared limit should contribute the processor count to a parallel group's
  `Min`. Coalescing before the `Min` makes the batch take the smaller of the processor count and the
  smallest declared limit, which is what the attribute documentation says a test without a
  declaration gets, and what the ungrouped path already does. It narrows a batch only when that batch
  holds at least one undeclared member and its smallest declared limit exceeds the processor count --
  a batch whose members all declare 16 still runs at 16 -- but that is still a scheduling change for
  suites passing today rather than a bug fix, which is why the documentation for 1.x describes the
  current behavior instead. The alternative is to declare the group limit deliberately authoritative,
  on the reading that an explicit `[ParallelGroup]` with an explicit limit is a statement about the
  group as a whole; that keeps today's behavior and makes the attribute docs say so. Resolved by
  documentation: the group limit is deliberately authoritative, and the `ParallelLimitAttribute`
  remarks say so as of PR #219. No code change.

## Deferred to the next major version

Breaking changes that could not ship in 1.x. All three shipped in 2.0.0, and the
`PublicAPI.Shipped.txt` baselines now freeze that surface; the section stays as the record of what
2.0.0 broke and why.

- [x] Unify the shared-instance caches behind `[ClassDataSource]` and `[ValuesFrom]` and wire
  disposal to session end. `ClassDataSourceExpander` and `CombinedDataSourceExpander` each keep
  their own `PerSession`/`PerAssembly`/`PerClass`/`Keyed` caches, so one data source type used
  through both attributes is instantiated twice, and nothing in the run lifecycle ever clears them:
  the `ClearSharedInstances`/`ClearClassInstances` methods that would dispose the instances have no
  caller. Both the instance identity and the disposal timing are user-observable, so unification is
  a breaking change. The `Clear*` methods stopped being public API with the demotion below, which
  leaves only the observable behavior to break. Documented as-is on both expanders for 1.x.
- [x] Demote the public types in the `NextUnit.Internal` namespace (`TestExecutionEngine`, the
  execution and expansion types) to `internal`. The namespace name already signals the intent, but
  the types ship as public API today. The descriptors and the delegates have to stay public: the
  generated registry in the user's assembly names them.
- [x] Remove the two `[Obsolete]` expectedMessage-validation overloads of `Assert.Throws` and
  `Assert.ThrowsAsync` (`Assert.Throws.cs`). They are unreachable with two positional arguments,
  which is why they were obsoleted in 1.x rather than deleted; their removal notice already promises
  NextUnit 2.0, so it is tracked here.

Delivered by deleting both overloads and the ten tests that covered them: eight that reached the
message validation through an explicit third argument, and two that pinned the `[Obsolete]` attribute
itself. The two-positional-argument binding they were confused with is unchanged and stays pinned by
the `TwoArgStringOverload` tests; what stops compiling is the explicit third argument and the named
`expectedMessage:` argument, which were the only ways to reach the validation. The removal was
recorded as two `*REMOVED*` entries in `src/NextUnit.Core/PublicAPI.Unshipped.txt`, and the matching
`PublicAPI.Shipped.txt` lines came out at the 2.0.0 release-prep promotion, per the Public API
Release Files step in `docs/RELEASE_PROCESS.md`. First of the breaking changes in this section to
land, which is what made the next release 2.0.0.

The `NextUnit.Internal` demotion covers nine types: `TestExecutionEngine`, `ITestExecutionSink`,
`TestOutcome`, `DependencyGraph` (with its nested `Node`), `ParallelScheduler`, `TestBatch`,
`TestDataExpander`, `ClassDataSourceExpander`, and `CombinedDataSourceExpander`. Two parts of the
plan above were wrong and were corrected against the compiler. The `InternalsVisibleTo` addition is
`NextUnit.TestAdapter.Tests`, not `NextUnit.Core.Tests`: `NextUnit.Core.Tests` covers `Assert` and
names no `NextUnit.Internal` type, while `NextUnit.TestAdapter.Tests` uses `TestDataExpander` and
`TestDataDescriptor` directly. `NextUnit.Benchmarks` needed the same grant for `DependencyGraph`,
`ParallelScheduler`, and `ITestExecutionSink`. The expanders are demoted rather than carved out: the
platform adapter reaches them through `InternalsVisibleTo`, so nothing about the carve-out was load
bearing, and demoting them takes the four uncalled `Clear*` methods out of the public surface, which
is what shrinks the cache-unification item above. What stays public is the contract the generated
code names: the descriptors, `TestCaseId`, `ArgumentConverter`, `AsyncDataSourceAdapter`,
`GeneratedTestRegistryStore`, `IGeneratedTestRegistry`, and the delegates. The delegate set was
settled empirically rather than by inspection: demoting `TestMethodDelegate`,
`TestMethodWithArgumentsDelegate`, `TestClassFactoryDelegate`, `AsyncDataSourceProviderDelegate`,
and `RetryPolicyFactoryDelegate` produced eleven CS0053 errors, because each one is the type of a
property on a descriptor that has to stay public. The removals were recorded as 59 `*REMOVED*`
entries in `src/NextUnit.Core/PublicAPI.Unshipped.txt` and promoted at the 2.0.0 release prep.

The cache unification is delivered as `SharedInstanceStore`, one process-wide store both expanders
call, keyed by sharing scope, data source type, and whatever else that scope shares by: the test
class for `PerClass`, the key for `Keyed`, the test assembly for `PerAssembly`, nothing more for
`PerSession`. The scope
is part of the key, so `PerAssembly` and `PerSession` still hold separate instances even though a
single-assembly run cannot tell the two lifetimes apart; collapsing them would change which tests
share an instance rather than only which attribute they arrived through, which is more than this item
agreed to break. `PerAssembly` gained the test assembly as a key component, which the scope's own
documentation always promised and the type-only key did not deliver in a multi-assembly VSTest run.
Entries are `Lazy` values rather than bare `GetOrAdd` factories, because
`ConcurrentDictionary` may run a losing factory and throw its result away, and an instance the store
never records is an instance nothing ever disposes; a failed creation is evicted so the next
expansion retries, as it did in 1.x. Disposal runs in reverse creation order, prefers
`IAsyncDisposable`, and reports every failure together. The four `Clear*` methods are deleted rather
than repurposed: they had no caller, and `ClearClassInstances` described a per-class release that no
lifecycle event ever reached. The wiring is asymmetric by necessity.
`SessionLifecycleRunner.RunTeardownAsync` disposes after the `[After(Session)]` hooks and through the
same failure aggregation, so a hook can still read a shared instance and a disposal failure reaches
the session result, with `NextUnitFramework.Dispose` as the backstop for a request that is cancelled
or fails and therefore never reaches session close. VSTest has no session boundary, so each adapter
operation owns what it created: the executor disposes at the end of a run and the discoverer at the
end of discovery, which costs a second instantiation when both happen in one process and is what
already happens whenever VSTest discovers and runs in separate processes. Neither host expands
concurrently with a cleanup - the platform closes a session after the requests that expand have
finished, and the adapter expands synchronously before its own cleanup - so the store arbitrates no
such race and an expansion that starts after a cleanup is a bug at its call site. Registration and
retirement are still serialized on one lock, which is what keeps the store's own state consistent:
an instance whose constructor was still running cannot be disposed and then handed to its caller, and
cannot register where no cleanup will see it either. `NextUnitFramework.Dispose` runs the cleanup
outside its idempotence guard so that second property has somewhere to land. One interleaving is
knowingly accepted rather than arbitrated: a cancelled request can leave a build detached and still
expanding, and the backstop in `Dispose` may release an instance that build is enumerating. Its rows
are already discarded and its failure already swallowed, whereas the alternative leaks a connection
or a container on every cancelled run, and arbitrating it means waiting on user code from
`Dispose`. Pinned by fifteen tests in
`tests/NextUnit.Generator.Tests/SharedInstanceStoreTests.cs`, written first against 1.x behavior,
where the same type through both attributes produced two instances and a keyed pair produced three,
and five more in `SessionLifecycleRunnerTests` for the ordering and the failure reporting.

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
