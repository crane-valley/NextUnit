# CLAUDE.md

- Source code, comments, logs, error messages: English
- PR titles, summaries, and comments: English
- Create feature branch → commit → push → PR (merge is done by humans)

## Commands

```bash
dotnet build                          # Build
dotnet format --verify-no-changes     # Format check
dotnet test samples/NextUnit.SampleTests/NextUnit.SampleTests.csproj  # Test
markdownlint --config .markdownlint.json <file>.md  # Markdown lint
```

## Release (src/*.cs changes only)

Ask user before release. Update these files in separate commit:

- `Directory.Build.props` - Version
- `Directory.Packages.props` - NextUnit.* versions
- `CHANGELOG.md` - Release notes
- `NUGET_README.md` - Version number and feature list
- `docs/GETTING_STARTED.md` - Package version in examples
- `docs/MIGRATION_FROM_XUNIT.md` - Package version in examples

After PR merge, create GitHub Release → auto-publishes to NuGet

## Key Files

- Solution: `NextUnit.slnx`
- Roadmap: `PLANS.md`
- Versions: `Directory.Build.props`, `Directory.Packages.props`
