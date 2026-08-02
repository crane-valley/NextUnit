# CLAUDE.md

## Validation

```bash
dotnet build NextUnit.slnx --configuration Release
dotnet format NextUnit.slnx --verify-no-changes
dotnet test --solution NextUnit.slnx --configuration Release --no-restore
markdownlint --config .markdownlint.json <file>.md
```

## Release (src/*.cs changes only)

Ask user before release. Update the nine release-time files in a separate commit;
the Version Update Checklist in `docs/RELEASE_PROCESS.md` is the single source of truth.

After PR merge, create GitHub Release → auto-publishes to NuGet

## Key Files

- Solution: `NextUnit.slnx`
- Roadmap: `PLANS.md`
- Versions: `Directory.Build.props`, `Directory.Packages.props`
