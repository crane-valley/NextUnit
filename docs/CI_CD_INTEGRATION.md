# CI/CD Integration

NextUnit test projects are Microsoft.Testing.Platform applications. The `NextUnit` package supplies
the generated entry point, framework registration, analyzers, and TRX reporter.

## Repository setup

The .NET 10 SDK selects a test runner at repository scope. Add `global.json` next to the solution:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

This repository includes that file. With it, use the MTP form of the .NET 10 CLI:

```bash
dotnet test
dotnet test --solution MySolution.slnx
dotnet test --project tests/MyProject.Tests/MyProject.Tests.csproj
```

Without `global.json`, a single project can always run directly:

```bash
dotnet run --project tests/MyProject.Tests/MyProject.Tests.csproj
```

## TRX reports

The main package includes `Microsoft.Testing.Extensions.TrxReport`:

```bash
dotnet test --project tests/MyProject.Tests/MyProject.Tests.csproj \
  --results-directory TestResults \
  --report-trx \
  --report-trx-filename results.trx
```

For `dotnet run`, put test-application arguments after `--`:

```bash
dotnet run --project tests/MyProject.Tests/MyProject.Tests.csproj -- \
  --results-directory TestResults \
  --report-trx \
  --report-trx-filename results.trx
```

## GitHub Actions

```yaml
name: Tests

on: [push, pull_request]

permissions:
  contents: read

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v6

    - uses: actions/setup-dotnet@v5
      with:
        dotnet-version: '10.0.x'

    - run: dotnet restore MySolution.slnx

    - name: Run tests
      run: |
        dotnet test --solution MySolution.slnx \
          --configuration Release \
          --no-restore \
          --results-directory TestResults \
          --report-trx

    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v6
      with:
        name: test-results
        path: TestResults/**/*.trx
```

## Azure Pipelines

```yaml
steps:
- task: UseDotNet@2
  inputs:
    packageType: sdk
    version: '10.0.x'

- script: dotnet restore MySolution.slnx
  displayName: Restore

- script: >
    dotnet test --solution MySolution.slnx
    --configuration Release
    --no-restore
    --results-directory $(Agent.TempDirectory)/TestResults
    --report-trx
  displayName: Test

- task: PublishTestResults@2
  condition: always()
  inputs:
    testResultsFormat: VSTest
    testResultsFiles: '$(Agent.TempDirectory)/TestResults/**/*.trx'
```

## Filtering

NextUnit supports command-line filters for direct project execution:

```bash
dotnet run --project tests/MyProject.Tests -- --test-name "*Calculator*"
dotnet run --project tests/MyProject.Tests -- --category Integration
dotnet run --project tests/MyProject.Tests -- --exclude-category Slow
```

Environment variables work in local shells and CI systems:

```bash
NEXTUNIT_INCLUDE_CATEGORIES=Unit dotnet run --project tests/MyProject.Tests
NEXTUNIT_EXCLUDE_TAGS=Flaky dotnet run --project tests/MyProject.Tests
```

## Dependency vulnerability gate

This section describes how the NextUnit repository gates its own dependencies. It is not something
the `NextUnit` package requires of your project, but the shape is reusable.

Two checks share one script, `.github/scripts/check-dependency-vulnerabilities.ps1`, which reads
`dotnet list <solution> package --vulnerable --include-transitive --format json`:

- The `Security Scan` job in `.github/workflows/dotnet.yml` runs on pull requests. It scans the head
  revision and the base revision, then fails only on vulnerabilities the base revision does not
  already have. Adding a vulnerable package, or moving a package into a vulnerable range, fails the
  pull request. An advisory published against a package the base branch already carries does not,
  because that finding belongs to everyone rather than to the pull request that happens to be open.
- The `Vulnerability Scan` job in `.github/workflows/nightly.yml` runs on the nightly schedule with
  no baseline, so it fails on every vulnerability the solution resolves. This is where packages
  already on `main` are held; a red nightly blocks nobody's pull request.

A vulnerability is identified by the package and the advisory, not by the package version, so
moving between two versions that share the same advisory is not treated as a new finding.

NuGet audit still runs on every restore, but `Directory.Build.props` keeps `NU1901` through `NU1904`
out of `TreatWarningsAsErrors`. As errors they would fail restore for every contributor the moment
an advisory was published, which is the outcome the two jobs above exist to avoid.

### Allowlist

Advisories that cannot be cleared by upgrading a package this repository controls go in
`.github/vulnerability-allowlist.txt`, one entry per line:

```text
GHSA-59j7-ghrg-fj52  2026-09-15  Reached only from the ASP.NET Core sample; upstream fix ships in 10.0.11.
```

The three fields are the GitHub advisory identifier, a UTC expiry date as `YYYY-MM-DD`, and a
reason. Blank lines and lines starting with `#` are ignored; any other line that does not match the
format fails the scan rather than being skipped, so a typo cannot silently change what is allowed.
Because the file is committed, adding or extending an entry goes through pull request review.

Expiry is what keeps the list honest:

- An entry is honored through the end of its expiry date and ignored afterwards, so an expired entry
  stops suppressing anything.
- The pull request gate reports an expired entry as a warning. It does not fail on expiry alone,
  since the date passing is not the fault of whoever opened the pull request.
- The nightly scan fails on an expired entry, and keeps failing until the entry is removed, renewed
  with a fresh reason, or made unnecessary.
- The nightly scan also reports entries that match no resolved package, which is the signal to
  delete them.

## CI guardrails

- Restore once, then use `--no-restore` for deterministic test jobs.
- Keep `global.json` at the solution root so local and CI runner selection match.
- Use `--minimum-expected-tests N` when an empty discovery result must fail the job.
- Upload TRX files under `if: always()` so failures retain diagnostics.
- Run Release builds for performance-sensitive suites.
