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

# Markdown lint (run when adding/modifying .md files)
markdownlint --config .markdownlint.json <changed-files>.md

# Build verification
dotnet build

# Format check (ensure code style compliance)
dotnet format --verify-no-changes

# Run tests
dotnet test samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj
dotnet test samples/ClassLibrary.Sample.Tests/ClassLibrary.Sample.Tests/ClassLibrary.Sample.Tests.csproj
dotnet test samples/Console.Sample.Tests/Console.Sample.Tests/Console.Sample.Tests.csproj
```

**Note**: When modifying markdown files (`.md`), always run `markdownlint` before committing.
The project uses `.markdownlint.json` for configuration.
Install markdownlint-cli globally if not available: `npm install -g markdownlint-cli`.

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

### 4. Handling PR Review Comments

1. **Check comments**: `gh api repos/crane-valley/NextUnit/pulls/<PR>/comments`
2. **Fix issues** if needed, commit with clear message
3. **Reply to each comment**: `gh api .../comments/<ID>/replies -X POST -f body="..."`
4. **Resolve threads** via GraphQL `resolveReviewThread` mutation
5. **Verify**: `gh pr checks <PR>` - all checks pass, no unresolved threads

### 5. Release Workflow (Source Code Changes Only)

When source code (`.cs` files in `src/`) is modified, ask the user:

> "Source code was modified. Would you like to create a release?"

If the user confirms, add a **separate commit** for version bump:

1. Update version in these files:
   - `Directory.Build.props` - `<Version>` property (e.g., `1.6.6` → `1.6.7`)
   - `Directory.Packages.props` - All `NextUnit.*` package versions
   - `CHANGELOG.md` - Add release notes for the new version
   - `docs/GETTING_STARTED.md` - Update version in code examples
   - `docs/MIGRATION_FROM_XUNIT.md` - Update version in code examples
   - `NUGET_README.md` - Update version if mentioned

2. Create a separate commit for version bump only:

   ```bash
   git add Directory.Build.props Directory.Packages.props CHANGELOG.md docs/ NUGET_README.md
   git commit -m "chore: Bump version to X.Y.Z"
   ```

3. Push and create PR as usual

4. After PR is merged, create a GitHub Release:
   - The release workflow (`.github/workflows/release.yml`) automatically publishes to NuGet

**Important**: Keep the version bump commit separate from feature/fix commits for clean history.

---

## Project Overview

NextUnit is a high-performance test framework for .NET 10+ with VS Test Explorer integration.

**Packages**: `NextUnit` (meta), `NextUnit.Core`, `NextUnit.Generator`, `NextUnit.TestAdapter`

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

## Key Principles

- **Documentation Sync**: Keep README.md, PLANS.md, CHANGELOG.md, docs/ updated with implementation
- **Zero-Reflection**: Maintain source generator approach for compile-time test discovery

## File Locations

- Solution file: `NextUnit.slnx`
- Package metadata: `Directory.Build.props`
- Central package versions: `Directory.Packages.props`
- Release workflow: `.github/workflows/release.yml`
- Package targets: `src/NextUnit/build/NextUnit.targets`
