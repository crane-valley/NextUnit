# CLAUDE.md

- Respond in Japanese
- Source code, comments, logs, error messages: English
- Create feature branch → commit → push → PR (merge is done by humans)
- **After completing work, update PLANS.md** (mark completed items, update version)

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

After PR merge, create GitHub Release → auto-publishes to NuGet

## Key Files

- Solution: `NextUnit.slnx`
- Roadmap: `PLANS.md`
- Versions: `Directory.Build.props`, `Directory.Packages.props`
