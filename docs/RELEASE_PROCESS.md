# NuGet Package Release Process

This document describes the complete process for releasing a new version of NextUnit NuGet packages.
This guide is designed to be read by both humans and Copilot agents to ensure consistent and complete releases.

## Overview

NextUnit consists of five NuGet packages:

- **NextUnit** (meta-package) - Aggregates all components
- **NextUnit.Core** - Core attributes, assertions, execution engine
- **NextUnit.Generator** - Source generator for test discovery
- **NextUnit.TestAdapter** - VSTest adapter for Visual Studio Test Explorer
- **NextUnit.Platform** - Microsoft.Testing.Platform integration (legacy)

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
   - Update: All five NextUnit package versions
     - `<PackageVersion Include="NextUnit" Version="X.Y.Z" />`
     - `<PackageVersion Include="NextUnit.Core" Version="X.Y.Z" />`
     - `<PackageVersion Include="NextUnit.Generator" Version="X.Y.Z" />`
     - `<PackageVersion Include="NextUnit.TestAdapter" Version="X.Y.Z" />`
     - `<PackageVersion Include="NextUnit.Platform" Version="X.Y.Z" />`

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

1. **tools/speed-comparison/README.md**
   - Location: `/tools/speed-comparison/README.md`
   - Update: `**NextUnit Version**: X.Y.Z` and `**Last Updated**: YYYY-MM-DD`

2. **tools/speed-comparison/BENCHMARKS.md**
   - Location: `/tools/speed-comparison/BENCHMARKS.md`
   - Update: `**NextUnit Version**: X.Y.Z` and `**Last Updated**: YYYY-MM-DD`

3. **tools/speed-comparison/UnifiedTests/UnifiedTests.csproj**
   - Location: `/tools/speed-comparison/UnifiedTests/UnifiedTests.csproj`
   - Update: `<PackageReference Include="NextUnit" Version="X.Y.Z" />` in the NextUnit configuration
   - **NOTE**: This file should be updated AFTER the new package version is published to NuGet,
     since it references the published package for benchmarking.
     Update this in a separate commit/PR after the release.

## Release Process Steps

### 1. Prepare Release Branch

```bash
# Create a new branch for the release
git checkout -b release/vX.Y.Z main
```

### 2. Update All Version References

Follow the Version Update Checklist above and update files 1-11.
Skip file #12 (UnifiedTests.csproj) for now - it will be updated after the package is published.

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
dotnet build -c Release

# Run tests
dotnet test tests/NextUnit.Platform.Tests
dotnet test tests/NextUnit.Generator.Tests
dotnet test samples/NextUnit.SampleTests

# Verify package builds
dotnet pack -c Release -o ./artifacts
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
- All five packages (NextUnit, NextUnit.Core, NextUnit.Generator, NextUnit.TestAdapter, NextUnit.Platform)
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

All NextUnit packages have `DevelopmentDependency=true` set:

- This prevents transitive dependency propagation
- Consuming projects won't expose NextUnit to their consumers
- Appropriate for test frameworks that are build-time only

This setting is in the individual `.csproj` files:

- `/src/NextUnit/NextUnit.csproj`
- `/src/NextUnit.Core/NextUnit.Core.csproj`
- `/src/NextUnit.Generator/NextUnit.Generator.csproj`
- `/src/NextUnit.TestAdapter/NextUnit.TestAdapter.csproj`
- `/src/NextUnit.Platform/NextUnit.Platform.csproj`

## Troubleshooting

### Issue: Version mismatch warnings during build

**Solution**: Ensure all 4 package versions in `Directory.Packages.props` are identical and match `Directory.Build.props`.

### Issue: NuGet push fails with "package already exists"

**Solution**: You cannot replace an existing package version. Increment the version number and try again.

### Issue: Tests fail after version update

**Solution**: The version update itself shouldn't affect tests.
Investigate what other changes were made. Revert to previous version if needed.

## For Copilot Agents

When asked to prepare a NuGet release:

1. **Understand the version increment**: Ask the user or infer from the changes (patch/minor/major)
2. **Use the checklist**: Update all 12 files/locations listed above
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
