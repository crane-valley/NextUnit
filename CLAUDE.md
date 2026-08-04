# CLAUDE.md

## Validation

```bash
dotnet build NextUnit.slnx --configuration Release
dotnet format NextUnit.slnx --verify-no-changes
dotnet test --solution NextUnit.slnx --configuration Release --no-restore
markdownlint --config .markdownlint.json <file>.md
```

## Release (src/*.cs changes only)

Ask user before release. Update the twelve release-time version files in a separate commit,
plus the two analyzer release files when `AnalyzerReleases.Unshipped.md` is not empty;
the Version Update Checklist in `docs/RELEASE_PROCESS.md` is the single source of truth.

After PR merge, create GitHub Release → auto-publishes to NuGet

## Key Files

- Solution: `NextUnit.slnx`
- Roadmap: `PLANS.md`
- Versions: `Directory.Build.props`, `Directory.Packages.props`
