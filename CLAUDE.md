# CLAUDE.md

## Validation

```bash
dotnet build NextUnit.slnx --configuration Release
dotnet format NextUnit.slnx --verify-no-changes
dotnet test --solution NextUnit.slnx --configuration Release --no-restore
markdownlint --config .markdownlint.json <file>.md
```

## Release (src/*.cs changes only)

Ask user before release. Update these files in separate commit:

- `Directory.Build.props` - Version
- `Directory.Packages.props` - NextUnit.* versions
- `CHANGELOG.md` - Release notes
- `README.md` - Version number and feature list
- `NUGET_README.md` - Version number and feature list
- `docs/GETTING_STARTED.md` - Package version in examples
- `docs/MIGRATION_FROM_XUNIT.md` - Package version in examples

After PR merge, create GitHub Release → auto-publishes to NuGet

## Key Files

- Solution: `NextUnit.slnx`
- Roadmap: `PLANS.md`
- Versions: `Directory.Build.props`, `Directory.Packages.props`
