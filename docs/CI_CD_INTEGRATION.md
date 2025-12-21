# CI/CD Integration Guide

This guide explains how to integrate NextUnit tests into various CI/CD systems and generate test reports in different formats.

## Table of Contents
- [TRX Report Format (Visual Studio)](#trx-report-format-visual-studio)
- [GitHub Actions Integration](#github-actions-integration)
- [Azure DevOps Integration](#azure-devops-integration)
- [Jenkins Integration](#jenkins-integration)
- [GitLab CI Integration](#gitlab-ci-integration)
- [General CI/CD Best Practices](#general-cicd-best-practices)

---

## TRX Report Format (Visual Studio)

TRX (Test Results XML) is the native format used by Visual Studio and Azure DevOps.

### Setup

1. Add the TRX report extension package:

```bash
dotnet add package Microsoft.Testing.Extensions.TrxReport
```

2. Enable TRX reporting in your test project's `Program.cs`:

```csharp
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Extensions;
using NextUnit.Platform;

var builder = await TestApplication.CreateBuilderAsync(args);
builder.AddNextUnit();
builder.AddTrxReportProvider();  // Add this line
using var app = await builder.BuildAsync();
return await app.RunAsync();
```

### Usage

Run tests with TRX report generation:

```bash
# Generate TRX report with default name
dotnet run --project YourTests.csproj -- --report-trx --results-directory ./TestResults

# Generate TRX report with custom filename
dotnet run --project YourTests.csproj -- --report-trx --report-trx-filename custom-results.trx --results-directory ./TestResults
```

The TRX file will be created in the specified results directory and can be:
- Viewed in Visual Studio (Test Explorer → Open Test Results)
- Published to Azure DevOps test runs
- Analyzed by various test reporting tools

---

## GitHub Actions Integration

### Basic Workflow

Create `.github/workflows/test.yml`:

```yaml
name: Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Run tests
      run: dotnet run --project tests/YourTests/YourTests.csproj
```

### With TRX Report Publishing

```yaml
name: Tests

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
        
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Run tests with TRX report
      run: |
        dotnet run --project tests/YourTests/YourTests.csproj -- \
          --report-trx \
          --results-directory ./TestResults
    
    - name: Publish Test Results
      uses: EnricoMi/publish-unit-test-result-action@v2
      if: always()
      with:
        files: |
          TestResults/**/*.trx
```

### Advanced: Matrix Testing

Test across multiple platforms and .NET versions:

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
        dotnet: ['10.0.x']
    
    runs-on: ${{ matrix.os }}
    
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: ${{ matrix.dotnet }}
    
    - name: Run tests
      run: dotnet run --project tests/YourTests/YourTests.csproj
```

---

## Azure DevOps Integration

### Basic Pipeline

Create `azure-pipelines.yml`:

```yaml
trigger:
- main
- develop

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'

steps:
- task: UseDotNet@2
  displayName: 'Use .NET 10'
  inputs:
    version: '10.0.x'
    
- task: DotNetCoreCLI@2
  displayName: 'Restore packages'
  inputs:
    command: 'restore'
    projects: '**/*.csproj'

- script: |
    dotnet run --project tests/YourTests/YourTests.csproj -- \
      --report-trx \
      --results-directory $(Agent.TempDirectory)/TestResults
  displayName: 'Run tests'

- task: PublishTestResults@2
  condition: succeededOrFailed()
  inputs:
    testResultsFormat: 'VSTest'
    testResultsFiles: '$(Agent.TempDirectory)/TestResults/**/*.trx'
    mergeTestResults: true
    failTaskOnFailedTests: true
```

### With Code Coverage

```yaml
steps:
- task: UseDotNet@2
  displayName: 'Use .NET 10'
  inputs:
    version: '10.0.x'

- task: DotNetCoreCLI@2
  displayName: 'Restore packages'
  inputs:
    command: 'restore'

- script: |
    dotnet run --project tests/YourTests/YourTests.csproj -- \
      --report-trx \
      --coverage \
      --coverage-output $(Agent.TempDirectory)/Coverage \
      --results-directory $(Agent.TempDirectory)/TestResults
  displayName: 'Run tests with coverage'

- task: PublishTestResults@2
  condition: succeededOrFailed()
  inputs:
    testResultsFormat: 'VSTest'
    testResultsFiles: '$(Agent.TempDirectory)/TestResults/**/*.trx'
    
- task: PublishCodeCoverageResults@1
  inputs:
    codeCoverageTool: 'Cobertura'
    summaryFileLocation: '$(Agent.TempDirectory)/Coverage/**/*.cobertura.xml'
```

---

## Jenkins Integration

### Jenkinsfile

```groovy
pipeline {
    agent any
    
    stages {
        stage('Restore') {
            steps {
                sh 'dotnet restore'
            }
        }
        
        stage('Test') {
            steps {
                sh '''
                    dotnet run --project tests/YourTests/YourTests.csproj -- \
                      --report-trx \
                      --results-directory ./TestResults
                '''
            }
        }
    }
    
    post {
        always {
            // Publish test results using xUnit plugin
            xunit (
                thresholds: [ skipped(failureThreshold: '0'), failed(failureThreshold: '0') ],
                tools: [ MSTest(pattern: 'TestResults/**/*.trx') ]
            )
        }
    }
}
```

### Declarative Pipeline with Parallel Stages

```groovy
pipeline {
    agent any
    
    stages {
        stage('Test') {
            parallel {
                stage('Unit Tests') {
                    steps {
                        sh '''
                            dotnet run --project tests/UnitTests/UnitTests.csproj -- \
                              --report-trx \
                              --results-directory ./TestResults/Unit
                        '''
                    }
                }
                
                stage('Integration Tests') {
                    steps {
                        sh '''
                            dotnet run --project tests/IntegrationTests/IntegrationTests.csproj -- \
                              --report-trx \
                              --results-directory ./TestResults/Integration
                        '''
                    }
                }
            }
        }
    }
    
    post {
        always {
            xunit (
                tools: [ MSTest(pattern: 'TestResults/**/*.trx') ]
            )
        }
    }
}
```

---

## GitLab CI Integration

### `.gitlab-ci.yml`

```yaml
image: mcr.microsoft.com/dotnet/sdk:10.0

stages:
  - test

test:
  stage: test
  script:
    - dotnet restore
    - dotnet run --project tests/YourTests/YourTests.csproj -- 
        --report-trx 
        --results-directory ./TestResults
  artifacts:
    when: always
    reports:
      junit: TestResults/**/*.trx
    paths:
      - TestResults/
    expire_in: 1 week
```

### With Coverage and Multiple Test Projects

```yaml
image: mcr.microsoft.com/dotnet/sdk:10.0

stages:
  - test
  - report

variables:
  TEST_RESULTS_DIR: "TestResults"

test:unit:
  stage: test
  script:
    - dotnet restore
    - dotnet run --project tests/UnitTests/UnitTests.csproj -- 
        --report-trx 
        --results-directory ./${TEST_RESULTS_DIR}/Unit
  artifacts:
    when: always
    paths:
      - ${TEST_RESULTS_DIR}/Unit/
    expire_in: 1 week

test:integration:
  stage: test
  script:
    - dotnet restore
    - dotnet run --project tests/IntegrationTests/IntegrationTests.csproj -- 
        --report-trx 
        --results-directory ./${TEST_RESULTS_DIR}/Integration
  artifacts:
    when: always
    paths:
      - ${TEST_RESULTS_DIR}/Integration/
    expire_in: 1 week

pages:
  stage: report
  dependencies:
    - test:unit
    - test:integration
  script:
    - echo "Test results available in artifacts"
  artifacts:
    paths:
      - ${TEST_RESULTS_DIR}/
```

---

## General CI/CD Best Practices

### 1. Fail Fast with Exit Codes

NextUnit automatically returns a non-zero exit code when tests fail, which CI systems detect:

```bash
dotnet run --project tests/YourTests.csproj
# Returns 0 if all tests pass, non-zero if any test fails
```

### 2. Minimum Expected Tests

Prevent accidental test discovery issues:

```bash
dotnet run --project tests/YourTests.csproj -- --minimum-expected-tests 50
```

This fails if fewer than 50 tests are discovered/executed.

### 3. Filtering Tests for Different Stages

```bash
# Run only unit tests
dotnet run --project tests/YourTests.csproj -- --category Unit

# Run only integration tests  
dotnet run --project tests/YourTests.csproj -- --category Integration

# Exclude slow tests in PR builds
dotnet run --project tests/YourTests.csproj -- --exclude-tag Slow
```

### 4. Parallel Execution Control

NextUnit respects `[ParallelLimit]` and `[NotInParallel]` attributes automatically. No special CI configuration needed.

### 5. Test Output for Debugging

Capture detailed output for failed tests:

```bash
dotnet run --project tests/YourTests.csproj -- --output Detailed
```

### 6. Timeout Protection

Prevent hanging tests:

```bash
dotnet run --project tests/YourTests.csproj -- --timeout 10m
```

### 7. Results Directory Organization

```bash
# Organize results by build number
dotnet run --project tests/YourTests.csproj -- \
  --report-trx \
  --results-directory ./TestResults/Build-${BUILD_NUMBER}
```

### 8. Combining with Code Coverage

When using code coverage tools (future NextUnit feature), structure your CI like this:

```bash
# Example for future coverage support
dotnet run --project tests/YourTests.csproj -- \
  --report-trx \
  --coverage \
  --coverage-output ./Coverage \
  --results-directory ./TestResults
```

---

## Environment Variables

NextUnit supports environment variables for filtering, useful in CI:

```bash
# Set in CI environment
export NEXTUNIT_INCLUDE_CATEGORIES=Unit,Integration
export NEXTUNIT_EXCLUDE_TAGS=Slow,Manual

# Run tests (filters applied automatically)
dotnet run --project tests/YourTests.csproj
```

Available environment variables:
- `NEXTUNIT_INCLUDE_CATEGORIES` - Comma-separated list of categories to include
- `NEXTUNIT_EXCLUDE_CATEGORIES` - Comma-separated list of categories to exclude
- `NEXTUNIT_INCLUDE_TAGS` - Comma-separated list of tags to include
- `NEXTUNIT_EXCLUDE_TAGS` - Comma-separated list of tags to exclude
- `NEXTUNIT_TEST_NAME` - Wildcard pattern for test names (supports `*` and `?`)
- `NEXTUNIT_TEST_NAME_REGEX` - Regular expression pattern for test names

**Note**: CLI arguments take precedence over environment variables.

---

## Troubleshooting

### Tests Not Discovered in CI

1. Ensure test project is built:
   ```bash
   dotnet build tests/YourTests.csproj
   ```

2. Check that source generator ran:
   ```bash
   ls tests/YourTests/obj/Debug/net10.0/generated/
   ```

3. Verify test assembly loads:
   ```bash
   dotnet run --project tests/YourTests.csproj -- --list-tests
   ```

### TRX File Not Generated

1. Verify TRX extension is installed:
   ```bash
   dotnet list package | grep TrxReport
   ```

2. Check that extension is registered in `Program.cs`:
   ```csharp
   builder.AddTrxReportProvider();
   ```

3. Ensure results directory exists or can be created:
   ```bash
   mkdir -p TestResults
   dotnet run --project tests/YourTests.csproj -- --report-trx --results-directory ./TestResults
   ```

### Permissions Issues in CI

Ensure the CI user has write access to the results directory:

```bash
# In CI script
mkdir -p TestResults
chmod 777 TestResults
dotnet run --project tests/YourTests.csproj -- --report-trx --results-directory ./TestResults
```

---

## Future Enhancements

The NextUnit team is working on additional CI/CD features:

- **JUnit XML Format** - Native support for Jenkins/GitLab CI
- **GitHub Actions Annotations** - Direct integration with GitHub Actions UI
- **TeamCity Service Messages** - Real-time test reporting in TeamCity
- **Code Coverage Integration** - Built-in coverage reporting
- **Test Retry** - Automatic retry for flaky tests

See [PLANS.md](../PLANS.md) for the complete roadmap.

---

## Examples Repository

Complete CI/CD examples are available in the NextUnit repository:
- GitHub Actions workflows: `.github/workflows/`
- Sample projects: `samples/`

---

## Additional Resources

- [Microsoft.Testing.Platform Documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform)
- [NextUnit Getting Started Guide](GETTING_STARTED.md)
- [NextUnit Best Practices](BEST_PRACTICES.md)
- [NextUnit Performance Guide](PERFORMANCE.md)
