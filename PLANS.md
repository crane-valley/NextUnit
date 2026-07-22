# NextUnit Development Plans

## Current state

**Current version**: 1.15.1 (stable)

**Last audited**: 2026-07-23

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

- [ ] Add a typed data-row representation usable by `TestData` and class data sources.
- [ ] Support per-row display name, categories, tags, and skip reason without changing the test
  method signature.
- [ ] Preserve row identity and metadata in Microsoft.Testing.Platform and the VSTest adapter so
  IDE selection and filtering behave consistently.
- [ ] Diagnose incompatible row value types and invalid metadata at build time where the source is
  statically knowable.
- [ ] Cover ordinary JIT, trimming, and Native AOT package-consumer paths.

#### Async and deferred data sources

- [ ] Accept cancellation-aware `IAsyncEnumerable<T>` member data and task/value-task-wrapped
  member collections without runtime reflection in the AOT path.
- [ ] Add explicit deferred enumeration for very large data sets so discovery can expose one
  placeholder and enumerate rows only during execution.
- [ ] Document the selection/filtering tradeoff of deferred rows and keep eager enumeration as the
  default.
- [ ] Benchmark discovery and execution with a 10,000-row source to prevent the feature from
  regressing startup or allocating an unbounded intermediate list.

### Priority 0 — One-command project creation

The .NET SDK ships MSTest, NUnit, and xUnit project templates, and TUnit publishes its own template.
NextUnit's package is now self-contained, but users still have to create and edit a project by hand.

- [ ] Publish a small `NextUnit.Templates` package with one C# `dotnet new nextunit` project
  template.
- [ ] Generate the minimal Microsoft.Testing.Platform project using only the `NextUnit` package and
  one passing example test.
- [ ] Verify install, creation, restore, build, discovery, execution, and uninstall from a clean
  NuGet cache in CI.
- Guardrail: do not add separate ASP.NET Core, Playwright, or Aspire templates until repeated user demand
  shows that the base template plus normal package references is insufficient.

### Priority 1 — Selective retry and retry observability

The existing `[Retry]` retries every non-timeout, non-skip failure. TUnit and current NUnit allow
retry decisions based on the exception, while MSTest exposes the current run count.

- [ ] Provide an extensible, async retry decision API with the exception, test context, and current
  attempt; keep today's retry-all behavior as the compatibility default.
- [ ] Expose the one-based retry attempt in `ITestContext` and include total attempts in the final
  failure output/result metadata.
- [ ] Prove cleanup, output, artifacts, cancellation, and `StateBag` semantics across attempts.
- Guardrail: avoid a separate statistics store; reporting should flow through existing test results and
  Microsoft.Testing.Platform.

### Priority 1 — Deterministic culture isolation

- [ ] Add assembly-, class-, and method-level culture control for current culture and UI culture,
  including an invariant-culture shorthand.
- [ ] Restore the original culture after pass, failure, timeout, and cancellation, and prevent
  culture-changing tests from contaminating concurrently running tests.
- [ ] Add representative `en-US`, `ja-JP`, and invariant test runs for formatting, parsing, display
  names, and assertion messages.

### Priority 1 — Performance regression detection

Weekly and pull-request round-robin comparisons already measure framework-dependent and Native AOT
executables and publish Markdown/JSON artifacts. The missing capability is a durable, noise-aware
decision rather than more schedules or report formats.

- [ ] Store a rolling history with runner, SDK, runtime, framework versions, commit, and raw samples.
- [ ] Compare like-for-like baselines and fail only on a repeated, statistically meaningful
  regression; never gate on one noisy median.
- Guardrail: keep the existing weekly schedule and path-filtered pull-request run. Do not add a second daily
  comparison workflow.

### Priority 2 — Adoption documentation

- [ ] Add concise NUnit-to-NextUnit and MSTest-to-NextUnit migration guides covering project setup,
  lifecycle, data sources, filtering, assertions, and deliberate non-equivalents.
- [ ] Link the guides from the README and NuGet README and compile every code sample in CI.
- Guardrail: defer an automated Roslyn migration tool until issues or real migrations demonstrate repeated
  mechanical work that documentation cannot solve.

### Priority 2 — Make dependency findings actionable

- [ ] Replace the non-blocking vulnerability scan with a check that fails for a newly introduced
  known vulnerable direct or transitive package.
- [ ] Support a narrow, reviewed, expiring allowlist for upstream vulnerabilities that cannot be
  removed immediately.
- Guardrail: keep Dependabot as the update mechanism; CodeQL and SBOM generation remain demand-triggered,
  not standing roadmap work.

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
