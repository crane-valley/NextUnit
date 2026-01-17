# Claude Code Instructions for NextUnit

## Development Workflow

All changes must follow this workflow:

### 1. Create a Work Branch

Never work directly on the main branch. Always create a work branch:

```bash
git checkout main
git pull origin main
git checkout -b <branch-type>/<description>
```

Branch name examples:
- `fix/docs-typo` - Documentation fixes
- `feat/assert-skip` - New feature
- `update/plans-roadmap` - Plan updates

### 2. Pre-Commit Verification

Before committing, always run:

```bash
# Documentation consistency check
# - Verify README.md and docs/ match the implementation
# - Verify source code XML comments are accurate

# Build verification
dotnet build

# Run tests
dotnet test samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj
dotnet test samples/ClassLibrary.Sample.Tests/ClassLibrary.Sample.Tests/ClassLibrary.Sample.Tests.csproj
dotnet test samples/Console.Sample.Tests/Console.Sample.Tests/Console.Sample.Tests.csproj
```

### 3. Commit and Create PR

Once all checks pass:

```bash
# Stage changes
git add <files>

# Create commit
git commit -m "<type>: <description>"

# Push branch
git push -u origin <branch-name>

# Create PR
gh pr create --title "<title>" --body "<description>"
```

Commit message types:
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation only changes
- `refactor:` - Code refactoring
- `test:` - Adding or modifying tests
- `chore:` - Build/tooling changes

---

## Project Overview

NextUnit is a modern, high-performance test framework for .NET 10+ with Visual Studio Test Explorer integration via VSTest adapter.

## Key Packages

- `NextUnit` - Meta-package (includes all dependencies)
- `NextUnit.Core` - Core attributes and assertions
- `NextUnit.Generator` - Source generator for test discovery
- `NextUnit.TestAdapter` - VSTest adapter for VS Test Explorer

## Before Making Changes

### Build Verification

Always verify the build succeeds before committing:

```bash
# Build all projects
dotnet build

# Build in Release configuration
dotnet build --configuration Release
```

### Test Verification

Run tests to ensure nothing is broken:

```bash
# Run sample tests (uses dotnet run for Microsoft.Testing.Platform)
dotnet run --project samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj

# Run tests via VSTest adapter (dotnet test)
dotnet test samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj
```

## Release Checklist

Before creating a new release, complete ALL of the following:

### 1. Pre-Release Verification

```bash
# Clean build
dotnet clean
dotnet build --configuration Release

# Run all sample tests
dotnet test samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj
dotnet test samples/ClassLibrary.Sample.Tests/ClassLibrary.Sample.Tests/ClassLibrary.Sample.Tests.csproj
dotnet test samples/Console.Sample.Tests/Console.Sample.Tests/Console.Sample.Tests.csproj

# Pack and verify packages locally
dotnet pack --configuration Release --output ./test-packages

# Verify all expected packages are created
ls ./test-packages/*.nupkg
# Should include:
# - NextUnit.Core.{version}.nupkg
# - NextUnit.Generator.{version}.nupkg
# - NextUnit.TestAdapter.{version}.nupkg
# - NextUnit.{version}.nupkg
```

### 2. Consumer Project Simulation

Test that the package works correctly for consumers:

```bash
# Create a temporary test project
mkdir /tmp/nextunit-test && cd /tmp/nextunit-test
dotnet new classlib -n TestProject
cd TestProject

# Add local package source and install
dotnet nuget add source /path/to/test-packages --name local-test
dotnet add package NextUnit --version {version}

# Verify build succeeds
dotnet build
```

### 3. Version Update Files

Update version in these files:
- `Directory.Build.props` - `<Version>` property
- `Directory.Packages.props` - All `NextUnit.*` package versions
- `CHANGELOG.md` - Add release notes

### 4. Solution File Check

Ensure all packable projects are in `NextUnit.slnx`:
- `src/NextUnit.Core/NextUnit.Core.csproj`
- `src/NextUnit.Generator/NextUnit.Generator.csproj`
- `src/NextUnit.TestAdapter/NextUnit.TestAdapter.csproj`
- `src/NextUnit/NextUnit.csproj`

### 5. Release Workflow Check

Verify `.github/workflows/release.yml` includes all packages in:
- Pack step
- Verify step

## Common Issues and Solutions

### Issue: Package not found on NuGet after release

**Cause:** Project not included in solution file or release workflow.

**Solution:**
1. Add project to `NextUnit.slnx`
2. Add pack step to `.github/workflows/release.yml`
3. Add verification step to release workflow

### Issue: Build errors in consumer projects (CS0234)

**Cause:** `NextUnit.targets` has incorrect settings.

**Solution:** Ensure `src/NextUnit/build/NextUnit.targets` only contains VSTest-compatible settings:
```xml
<Project>
  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
</Project>
```

### Issue: Tests not discovered in VS Test Explorer

**Cause:** Missing TestAdapter or incorrect project configuration.

**Solution:**
1. Ensure `NextUnit.TestAdapter` is included in the package
2. Verify `IsTestProject=true` in consuming project
3. Check that `Microsoft.NET.Test.Sdk` is referenced

## Architecture Notes

### VSTest Mode (Current)

- Uses `ITestDiscoverer` and `ITestExecutor` interfaces
- Works with `dotnet test` and Visual Studio Test Explorer
- Test projects are class libraries (not executables)
- No `Program.cs` required

### Microsoft.Testing.Platform Mode (Legacy)

- Uses Microsoft.Testing.Platform for test execution
- Test projects are executables with `Program.cs`
- Run via `dotnet run`
- Still supported for advanced scenarios

## File Locations

- Solution file: `NextUnit.slnx`
- Package metadata: `Directory.Build.props`
- Central package versions: `Directory.Packages.props`
- Release workflow: `.github/workflows/release.yml`
- Package targets: `src/NextUnit/build/NextUnit.targets`
