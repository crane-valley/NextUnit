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

## CI guardrails

- Restore once, then use `--no-restore` for deterministic test jobs.
- Keep `global.json` at the solution root so local and CI runner selection match.
- Use `--minimum-expected-tests N` when an empty discovery result must fail the job.
- Upload TRX files under `if: always()` so failures retain diagnostics.
- Run Release builds for performance-sensitive suites.
