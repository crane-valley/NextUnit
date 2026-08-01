# NuGet Package Release Process

This document describes the complete process for releasing a new version of NextUnit NuGet packages.
This guide is designed to be read by both humans and Copilot agents to ensure consistent and complete releases.

## Overview

NextUnit consists of six NuGet packages:

- **NextUnit** (meta-package) - Aggregates all components
- **NextUnit.Core** - Core attributes, assertions, execution engine
- **NextUnit.Generator** - Source generator for test discovery
- **NextUnit.TestAdapter** - VSTest adapter for Visual Studio Test Explorer
- **NextUnit.Platform** - Microsoft.Testing.Platform integration (legacy)
- **NextUnit.AspNetCore** - ASP.NET Core integration testing support

All packages share the same version number and are released together.

## Pre-Release Checklist

Before starting the release process:

- [ ] All tests pass locally (`dotnet test` on test projects)
- [ ] All CI/CD checks pass on the main branch
- [ ] CHANGELOG.md has been updated with release notes for the new version
- [ ] Any new features have been documented in relevant documentation files

## Version Update Checklist

When releasing a new version (e.g., updating from 1.6.0 to 1.6.1), the following files **MUST** be updated:

### Core Version Files

1. **Directory.Build.props**
   - Location: `/Directory.Build.props`
   - Update: `<Version>X.Y.Z</Version>` in the Shared Package Metadata section
   - Example: `<Version>1.6.1</Version>`

2. **Directory.Packages.props**
   - Location: `/Directory.Packages.props`
   - Update: All six NextUnit package versions
     - `<PackageVersion Include="NextUnit" Version="X.Y.Z" />`
     - `<PackageVersion Include="NextUnit.Core" Version="X.Y.Z" />`
     - `<PackageVersion Include="NextUnit.Generator" Version="X.Y.Z" />`
     - `<PackageVersion Include="NextUnit.TestAdapter" Version="X.Y.Z" />`
     - `<PackageVersion Include="NextUnit.Platform" Version="X.Y.Z" />`
     - `<PackageVersion Include="NextUnit.AspNetCore" Version="X.Y.Z" />`

### Documentation Files

1. **README.md**
   - Location: `/README.md`
   - Update: `**Current Version**: X.Y.Z (Stable)` near the top of the file

2. **NUGET_README.md**
   - Location: `/NUGET_README.md`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in the Quick Start section

3. **CHANGELOG.md**
   - Location: `/CHANGELOG.md`
   - Add new version section above the previous version
   - Format:

     ```markdown
     ## [X.Y.Z] - YYYY-MM-DD

     ### Added/Changed/Fixed/Removed
     - Description of changes
     ```

4. **PLANS.md**
   - Location: `/PLANS.md`
   - Add new version to the Version History table
   - Format: `| X.Y.Z | YYYY-MM-DD | ✅ Released | Brief description |`

### User Documentation

1. **docs/GETTING_STARTED.md**
   - Location: `/docs/GETTING_STARTED.md`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in examples

2. **docs/MIGRATION_FROM_XUNIT.md**
   - Location: `/docs/MIGRATION_FROM_XUNIT.md`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in examples

3. **docs/PERFORMANCE.md**
   - Location: `/docs/PERFORMANCE.md`
   - Update: `**NextUnit Version**: X.Y.Z` in the version information section

### Tools and Benchmarks

Nothing under `tools/speed-comparison/` requires a release-time update.

`tools/speed-comparison/UnifiedTests/UnifiedTests.csproj` reaches NextUnit through `ProjectReference`,
not `PackageReference`, so the comparison always measures the current checkout. This is deliberate:
the project carries the guardrail comment "Benchmark the current checkout instead of a stale published
package." introduced by PR #154. Repointing it at a published package would reintroduce the stale
measurements that change fixed.

Because the benchmark tracks the checkout rather than a release, its outputs are versioned by the run
that produced them:

- The comparison table in `docs/PERFORMANCE.md` records its provenance as a checkout - for example,
  "PR #160 checkout (1.15.1 assembly)" - alongside the SDK and runtime versions used.
- Raw results live in `tools/speed-comparison/results/`.
- Refreshes run through `.github/workflows/speed-comparison.yml` (manual `workflow_dispatch`, the
  scheduled weekly run, or a PR touching the benchmark paths), which uploads the measurements as
  workflow artifacts.

Refresh the numbers when the methodology or the competitor set changes, not once per release.

## Release Process Steps

### 1. Prepare Release Branch

```bash
# Create a new branch for the release
git checkout -b release/vX.Y.Z main
```

### 2. Update All Version References

Follow the Version Update Checklist above and update all nine files.

**Automation Tip for Copilot Agents:**
You can use the `edit` tool to make multiple updates in parallel for efficiency.

### 3. Update CHANGELOG.md

Add a new section for the release with:

- Version number and date
- Category sections (Added/Changed/Fixed/Removed) as appropriate
- Clear description of all changes
- Any breaking changes should be highlighted
- Migration notes if applicable

### 4. Verify Changes

```bash
# Check all modified files
git status

# Review the diff
git diff

# Verify no unintended changes
git diff | grep -E "^\+.*1\.[0-9]+\.[0-9]+" # Should show all new version references
```

### 5. Build and Test

```bash
# Build all projects
dotnet build NextUnit.slnx --configuration Release

# Run tests
dotnet test --solution NextUnit.slnx --configuration Release --no-restore

# Verify package builds
dotnet pack src/NextUnit.Core/NextUnit.Core.csproj -c Release -o ./artifacts
dotnet pack src/NextUnit.Generator/NextUnit.Generator.csproj -c Release -o ./artifacts
dotnet pack src/NextUnit.Platform/NextUnit.Platform.csproj -c Release -o ./artifacts
dotnet pack src/NextUnit.TestAdapter/NextUnit.TestAdapter.csproj -c Release -o ./artifacts
dotnet pack src/NextUnit.AspNetCore/NextUnit.AspNetCore.csproj -c Release -o ./artifacts
dotnet pack src/NextUnit/NextUnit.csproj -c Release -o ./artifacts
```

### 6. Commit and Create PR

```bash
git add .
git commit -m "Release vX.Y.Z"
git push origin release/vX.Y.Z

# Create PR to main branch
# PR title: "Release vX.Y.Z"
# PR description: Copy the CHANGELOG entry for this version
```

### 7. Merge PR

After PR approval, merge to main branch.

### 8. Create GitHub Release (Automated Publishing)

Creating a release on GitHub automatically triggers the NuGet package publishing via GitHub Actions (`.github/workflows/release.yml`):

1. Go to: <https://github.com/crane-valley/NextUnit/releases/new>
2. Click "Choose a tag" and create a new tag: `vX.Y.Z`
3. Release title: `NextUnit vX.Y.Z`
4. Description: Copy the CHANGELOG entry for this version
5. Publish release

**What happens automatically:**

- GitHub Actions workflow (`.github/workflows/release.yml`) is triggered
- Packages are built and packed
- All six packages (NextUnit, NextUnit.Core, NextUnit.Generator, NextUnit.TestAdapter,
  NextUnit.Platform, NextUnit.AspNetCore)
  are published to NuGet.org using GitHub OIDC authentication
- No manual API key or `dotnet nuget push` commands needed

### 9. Verify Release

- [ ] NuGet packages are visible at <https://www.nuget.org/packages/NextUnit/>
- [ ] GitHub release is created
- [ ] Documentation on main branch shows correct version
- [ ] Badge on README.md shows correct version

## Post-Release

1. Announce the release (if applicable):
   - GitHub Discussions
   - Twitter/Social Media
   - Discord/Slack channels

2. Monitor for issues:
   - Watch for GitHub issues related to the new release
   - Check NuGet download stats
   - Monitor CI/CD for any failures

### Bump the PackageSmoke Fallback Version

The two package smoke projects consume NextUnit as a published package rather than a project
reference:

- `tests/NextUnit.PackageSmoke/NextUnit.PackageSmoke.csproj`
- `tests/NextUnit.AspNetCore.PackageSmoke/NextUnit.AspNetCore.PackageSmoke.csproj`

Each resolves its version through two conditional properties:

```xml
<NextUnitPackageSmokeVersion Condition="'$(UseLocalNextUnitPackage)' == 'true'">$(NextUnitVersion)</NextUnitPackageSmokeVersion>
<NextUnitPackageSmokeVersion Condition="'$(NextUnitPackageSmokeVersion)' == ''">X.Y.Z</NextUnitPackageSmokeVersion>
```

The second line is the fallback, and it must name a version that is already on nuget.org. GitHub's
automatic dependency submission restores these projects without the local package feed, so a fallback
pointing at an unpublished version breaks that job.

This bump belongs in its own chore PR after the release, never in the release PR, because the version
has to be live on nuget.org first. Recent examples: #172, #179, #195.

#### 1. Wait for nuget.org to index the new version

Check every package, not just the two the smoke projects name directly. `NextUnit` depends on
`NextUnit.Core` and `NextUnit.Platform` at the same version, and `NextUnit.AspNetCore` depends on
`NextUnit.Core`, so a consumer restore still fails while any one of them is unindexed. The indexes do
not all become visible at the same moment.

```bash
for pkg in nextunit nextunit.core nextunit.generator nextunit.testadapter nextunit.platform nextunit.aspnetcore; do
  printf '%s: ' "$pkg"
  curl -s "https://api.nuget.org/v3-flatcontainer/$pkg/index.json" | grep -c '"X.Y.Z"'
done
```

Indexing lags the GitHub release by several minutes. Do not open the bump PR until all six lines
report `1`.

#### 2. Update the fallback in both projects

Set the fallback line to the released version in both csproj files. Nothing else changes.

#### 3. Verify with a direct build of both smoke projects

```bash
dotnet build tests/NextUnit.PackageSmoke/NextUnit.PackageSmoke.csproj --configuration Release
dotnet build tests/NextUnit.AspNetCore.PackageSmoke/NextUnit.AspNetCore.PackageSmoke.csproj --configuration Release
```

Both must finish with 0 warnings and 0 errors.

Do not substitute a solution build or rely on CI here, because neither one compiles the line you
changed:

- Neither smoke project is a member of `NextUnit.slnx`, so `dotnet build NextUnit.slnx` never touches
  them.
- Every smoke invocation in `.github/workflows/dotnet.yml` and `.github/workflows/release.yml` passes
  `-p:UseLocalNextUnitPackage=true`, which selects the first condition and bypasses the fallback.

A local build with `UseLocalNextUnitPackage` left unset is the only path that exercises the fallback.

**Cold cache caveat**: the builds above prove the fallback line compiles, not that nuget.org actually
serves the version. A warm cache satisfies them on its own, and deleting a package directory or two
does not help because the cached transitive dependencies still cover the restore. Three separate
caches have to be bypassed at once - the global packages folder, the HTTP cache, and any extra feed
configured in NuGet.config - so restore explicitly against nuget.org before building:

```bash
tmp=$(mktemp -d)
for proj in tests/NextUnit.PackageSmoke/NextUnit.PackageSmoke.csproj \
            tests/NextUnit.AspNetCore.PackageSmoke/NextUnit.AspNetCore.PackageSmoke.csproj; do
  NUGET_PACKAGES=$tmp dotnet restore "$proj" \
    --source https://api.nuget.org/v3/index.json --no-http-cache
  NUGET_PACKAGES=$tmp dotnet build "$proj" --configuration Release --no-restore
done
```

`--source` overrides the configured feeds, `--no-http-cache` stops NuGet from replaying a previously
downloaded response, and `NUGET_PACKAGES` relocates the extracted packages. If this passes, the
version is genuinely public.

## Version Numbering Guidelines

NextUnit follows [Semantic Versioning](https://semver.org/):

- **Major (X.0.0)**: Breaking changes, incompatible API changes
- **Minor (1.X.0)**: New features, backward-compatible additions
- **Patch (1.6.X)**: Bug fixes, backward-compatible fixes

Examples:

- `1.6.0` → `1.6.1`: Bug fixes, configuration changes (PATCH)
- `1.6.0` → `1.7.0`: New assertions, new features (MINOR)
- `1.6.0` → `2.0.0`: Breaking API changes (MAJOR)

## Package Configuration Notes

Only the analyzer and source-generator packages use
`DevelopmentDependency=true`:

- `/src/NextUnit.Analyzers/NextUnit.Analyzers.csproj`
- `/src/NextUnit.Generator/NextUnit.Generator.csproj`

Runtime packages, including `NextUnit.Platform`, must remain normal package
dependencies so their compile and runtime assets reach consuming test projects.

## Troubleshooting

### Issue: Version mismatch warnings during build

**Solution**: Ensure all six package versions in `Directory.Packages.props` are identical and match `Directory.Build.props`.

### Issue: NuGet push fails with "package already exists"

**Solution**: You cannot replace an existing package version. Increment the version number and try again.

### Issue: Tests fail after version update

**Solution**: The version update itself shouldn't affect tests.
Investigate what other changes were made. Revert to previous version if needed.

## For Copilot Agents

When asked to prepare a NuGet release:

1. **Understand the version increment**: Ask the user or infer from the changes (patch/minor/major)
2. **Use the checklist**: Update all nine files/locations listed above
3. **Maintain consistency**: Ensure all version references are identical
4. **Update dates**: Use current date for CHANGELOG.md and other dated fields
5. **Preserve formatting**: Match existing formatting in all files
6. **Verify completeness**: Check that no files were missed using:

   ```bash
   grep -r "OLD_VERSION" --include="*.md" --include="*.props" --include="*.csproj"
   ```

### Example Commands for Agents

```bash
# Find all version references (replace X.Y.Z with current version)
grep -r "1\.6\.0" --include="*.md" --include="*.props" --include="*.csproj"

# Verify no mixed versions exist
grep -rE "1\.[0-9]+\.[0-9]+" --include="*.md" --include="*.props" --include="*.csproj" | grep -v "1.6.1" | grep -v ".git"
```

## Summary

This document provides a complete checklist for releasing NextUnit NuGet packages. Following this process ensures:

- All version references are updated consistently
- Documentation remains accurate
- Users can smoothly upgrade to new versions
- Future releases can be automated with confidence

For questions or improvements to this process, please open an issue or discussion on GitHub.
