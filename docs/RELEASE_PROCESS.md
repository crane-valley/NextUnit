# NuGet Package Release Process

This document describes the complete process for releasing a new version of NextUnit NuGet packages.
This guide is designed to be read by both humans and Copilot agents to ensure consistent and complete releases.

## Overview

NextUnit consists of seven NuGet packages:

- **NextUnit** (meta-package) - Aggregates all components
- **NextUnit.Core** - Core attributes, assertions, execution engine
- **NextUnit.Generator** - Source generator for test discovery
- **NextUnit.TestAdapter** - VSTest adapter for Visual Studio Test Explorer
- **NextUnit.Platform** - Microsoft.Testing.Platform integration
- **NextUnit.AspNetCore** - ASP.NET Core integration testing support
- **NextUnit.Templates** - `dotnet new nextunit` project template

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
   - Update: `<NextUnitVersion>X.Y.Z</NextUnitVersion>` in the opening `PropertyGroup`
   - Example: `<NextUnitVersion>1.6.1</NextUnitVersion>`
   - This one property feeds every in-repo `PackageVersion` for a NextUnit package
     (`NextUnit`, `NextUnit.Core`, `NextUnit.Generator`, `NextUnit.TestAdapter`,
     `NextUnit.Platform`, `NextUnit.AspNetCore`), each of which reads `$(NextUnitVersion)` rather
     than carrying its own literal, so the six cannot drift apart.
   - `NextUnit.Templates` is deliberately absent. Central package management describes packages this
     repository *consumes*, and nothing here references the template package; it takes its version
     from `Directory.Build.props` like every other package.

### Template Content Files

1. **src/NextUnit.Templates/templates/NextUnit-CSharp/Company.TestProject1.csproj.template**
   - Location: `/src/NextUnit.Templates/templates/NextUnit-CSharp/Company.TestProject1.csproj.template`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />`
   - This file ships as package content and is generated into a project outside this repository, so
     it cannot resolve its version through `Directory.Packages.props` the way an in-repo project
     does. The literal is the only place the generated project learns which NextUnit to restore.
   - The `.template` extension keeps the file off every repository-wide `*.csproj` sweep; the
     template engine renames it on creation. Without it, GitHub's automatic dependency submission
     restores the file against nuget.org, which fails on `NU1008` under central package management
     and would fail again on every release PR because the version being released is not published
     yet.
   - The `template-smoke` job in `.github/workflows/dotnet.yml` compares this literal against
     `Directory.Build.props` and fails the build when they diverge, so a forgotten bump surfaces on
     the release PR rather than in a user's first `dotnet new nextunit`.

### Analyzer Release Files

There are two independent pairs, one per diagnostic-producing project:

1. **src/NextUnit.Analyzers/AnalyzerReleases.Shipped.md** and
   **src/NextUnit.Analyzers/AnalyzerReleases.Unshipped.md**, holding the `NU00xx` analyzer rules
2. **src/NextUnit.Generator/AnalyzerReleases.Shipped.md** and
   **src/NextUnit.Generator/AnalyzerReleases.Unshipped.md**, holding the `NEXTUNIT0xx` generator
   rules

Handle each pair on its own:

- Move every rule listed in that project's `AnalyzerReleases.Unshipped.md` into a new
  `## Release X.Y.Z` section at the end of the same project's `AnalyzerReleases.Shipped.md`, then
  reset the unshipped file to its empty state, the single line `; No unshipped rule changes.`
- Skip a pair whose unshipped file lists no rules, which is the case for a release that adds no
  diagnostics to that project. Note that the empty state is not a zero-byte file but that single
  marker line, so test for rule entries rather than for emptiness.
- The two ledgers advance independently, and neither tracks the other: the analyzer side has
  sections for `1.9.0`, `1.16.0`, and `1.19.0`, the generator side for `1.0.0`, `1.9.0`, `1.10.0`,
  and `1.11.0`.
- These files record which release first shipped each diagnostic, so leaving a rule unshipped
  after publishing it loses that provenance permanently. `RS2008` is satisfied by an entry in
  either file and therefore does not catch the omission; the build stays green either way.
- Added to this checklist during the 1.19.0 release, the first release to add rules since PR #171
  introduced the analyzer pair and PR #182 the generator pair, and so the first time the promotion
  was needed. Only the analyzer pair had rules to promote in that cycle.

### Documentation Files

1. **README.md**
   - Location: `/README.md`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in the Project Configuration
     snippet under Quick Start. That snippet is the only version literal in the file; the NuGet
     badge resolves the published version on its own and needs no edit.

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
   - Update: `**Current version**: X.Y.Z (stable)` in the Current state section
   - This one line is the whole release-time edit. The Completed summary table below it
     (`| Version | Shipped capability |`) groups releases into coarse ranges such as `1.15.x`, so it
     gains a row only when a release ships a capability worth summarizing, not once per version.
     Releases #169, #175, and #184 each changed the Current version line and nothing else in this file.

### User Documentation

1. **docs/GETTING_STARTED.md**
   - Location: `/docs/GETTING_STARTED.md`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in examples

2. **docs/MIGRATION_FROM_XUNIT.md**
   - Location: `/docs/MIGRATION_FROM_XUNIT.md`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in examples

3. **docs/MIGRATION_FROM_NUNIT.md**
   - Location: `/docs/MIGRATION_FROM_NUNIT.md`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in the "After" project file

4. **docs/MIGRATION_FROM_MSTEST.md**
   - Location: `/docs/MIGRATION_FROM_MSTEST.md`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in the "After" project file

5. **samples/ClassLibrary.Sample.Tests/README.md**
   - Location: `/samples/ClassLibrary.Sample.Tests/README.md`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in the standalone-project snippet
   - Easy to miss because the sample itself resolves versions through `Directory.Packages.props`;
     only this one snippet is pinned. Releases #169, #175, and #184 all bumped it.

`docs/PERFORMANCE.md` is deliberately absent from this list. It once carried a
`**NextUnit Version**: X.Y.Z` line, but PR #152 removed it in favor of a per-framework Version
column in the comparison table, so there is no release-time marker left to bump. Releases #169, #175,
and #184 did not touch the file. See Tools and Benchmarks below for the versions it does record.

### Tools and Benchmarks

Nothing under `tools/speed-comparison/` requires a release-time update.

`tools/speed-comparison/UnifiedTests/UnifiedTests.csproj` reaches NextUnit through `ProjectReference`,
not `PackageReference`, so the comparison always measures the current checkout. This is deliberate:
the project carries the guardrail comment "Benchmark the current checkout instead of a stale published
package." Repointing it at a published package would reintroduce the stale measurements that PR #154
was written to eliminate.

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

Follow the Version Update Checklist above and update all twelve version-reference files
(two core version files, one template content file, four documentation files,
five user documentation files), plus each analyzer release ledger pair whose
`AnalyzerReleases.Unshipped.md` lists any rules.

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
dotnet pack src/NextUnit.Templates/NextUnit.Templates.csproj -c Release -o ./artifacts
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
- All seven packages (NextUnit, NextUnit.Core, NextUnit.Generator, NextUnit.TestAdapter,
  NextUnit.Platform, NextUnit.AspNetCore, NextUnit.Templates)
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

Checking the two packages the smoke projects name directly is not enough. `NextUnit` depends on
`NextUnit.Core` and `NextUnit.Platform` at the same version, and `NextUnit.AspNetCore` depends on
`NextUnit.Core`, so the restore still fails while any of those four is unindexed, and the indexes do
not all become visible at the same moment. The loop below covers all seven published packages, which
also confirms the release itself completed. The remaining three are a release check rather than a
restore blocker, for different reasons: `NextUnit.Generator` is bundled into `NextUnit` as an
analyzer asset instead of being declared as a dependency, `NextUnit.TestAdapter` is an ordinary
package that the smoke projects simply do not reference, and `NextUnit.Templates` carries no
dependencies at all.

Save this as a script and run it rather than pasting it into an interactive shell, where the closing
`test` would end your session:

```bash
set -e
version=X.Y.Z
missing=0
for pkg in nextunit nextunit.core nextunit.generator nextunit.testadapter nextunit.platform nextunit.aspnetcore nextunit.templates; do
  if curl -sf "https://api.nuget.org/v3-flatcontainer/$pkg/index.json" | grep -q "\"$version\""; then
    echo "ok      $pkg"
  else
    echo "MISSING $pkg"
    missing=1
  fi
done
test "$missing" -eq 0
```

Indexing lags the GitHub release by several minutes. Do not open the bump PR until every line reads
`ok`; the closing `test` makes the script exit non-zero if any package is still missing, so a partial
result cannot pass unnoticed.

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

No checked-in workflow exercises the fallback, so this direct build is the only verification you
control. GitHub's automatic dependency submission does restore through it, but that job runs after the
merge and is not a pre-merge gate.

**Cold cache caveat**: the builds above prove the fallback line compiles, not that nuget.org actually
serves the version. A warm cache satisfies them on its own, and deleting a package directory or two
does not help because the cached transitive dependencies still cover the restore. Three separate
caches have to be bypassed at once - the global packages folder, the HTTP cache, and any extra feed
configured in NuGet.config - so restore explicitly against nuget.org before building:

Run this as a script too:

```bash
set -e
version=X.Y.Z
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
for proj in tests/NextUnit.PackageSmoke/NextUnit.PackageSmoke.csproj \
            tests/NextUnit.AspNetCore.PackageSmoke/NextUnit.AspNetCore.PackageSmoke.csproj; do
  test "$(dotnet msbuild "$proj" -getProperty:NextUnitPackageSmokeVersion | tr -d '\r')" = "$version"
  NUGET_PACKAGES=$tmp dotnet restore "$proj" \
    --source https://api.nuget.org/v3/index.json --no-http-cache
  NUGET_PACKAGES=$tmp dotnet build "$proj" --configuration Release --no-restore
done
```

The `-getProperty` assertion is what makes this a check on the new version. Without it, a csproj you
forgot to bump would quietly restore its old fallback and the script would still pass. The `tr -d
'\r'` guards the comparison against a CRLF line ending, which some Windows shells leave on the
captured value. `set -e` then
stops on the first failing project, so one bad fallback cannot be masked by the other succeeding, and
the `trap` still runs on that early exit so the throwaway package directory is removed either way.

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

**Solution**: Ensure `<NextUnitVersion>` in `Directory.Packages.props` matches `<Version>` in
`Directory.Build.props`. The six NextUnit `PackageVersion` items all read that property, so a
mismatch between the two files is the only way they can disagree.

### Issue: NuGet push fails with "package already exists"

**Solution**: You cannot replace an existing package version. Increment the version number and try again.

### Issue: Tests fail after version update

**Solution**: The version update itself shouldn't affect tests.
Investigate what other changes were made. Revert to previous version if needed.

## For Copilot Agents

When asked to prepare a NuGet release:

1. **Understand the version increment**: Ask the user or infer from the changes (patch/minor/major)
2. **Use the checklist**: Update all twelve version-reference files/locations listed above, and
   promote each analyzer release ledger pair whose unshipped file lists any rules
3. **Maintain consistency**: Ensure all version references are identical
4. **Update dates**: Use current date for CHANGELOG.md and other dated fields
5. **Preserve formatting**: Match existing formatting in all files
6. **Verify completeness**: Check that no files were missed using:

   ```bash
   grep -r "OLD_VERSION" --include="*.md" --include="*.props" --include="*.csproj" --include="*.csproj.template"
   ```

### Example Commands for Agents

```bash
# Find all version references (replace X.Y.Z with current version)
grep -r "1\.6\.0" --include="*.md" --include="*.props" --include="*.csproj" --include="*.csproj.template"

# Verify no mixed versions exist
grep -rE "1\.[0-9]+\.[0-9]+" --include="*.md" --include="*.props" --include="*.csproj" --include="*.csproj.template" | grep -v "1.6.1" | grep -v ".git"
```

## Summary

This document provides a complete checklist for releasing NextUnit NuGet packages. Following this process ensures:

- All version references are updated consistently
- Documentation remains accurate
- Users can smoothly upgrade to new versions
- Future releases can be automated with confidence

For questions or improvements to this process, please open an issue or discussion on GitHub.
