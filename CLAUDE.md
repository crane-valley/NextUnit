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
plus each analyzer release ledger pair whose `AnalyzerReleases.Unshipped.md` lists any rules
and each public API pair whose `PublicAPI.Unshipped.txt` lists any entries;
the Version Update Checklist in `docs/RELEASE_PROCESS.md` is the single source of truth.

After PR merge, create GitHub Release → auto-publishes to NuGet

Cadence guardrail (`docs/RELEASE_PROCESS.md`, "Release cadence"):

- Releases follow that cadence rule, not "the work is done, so ship it".
- Never open a release PR within 30 days of the previous tag without `RELEASE-CADENCE-EXCEPTION:
  <reason>` in the PR body. An urgent PATCH or MINOR needs that token alone.
- Never open a release PR that bumps MAJOR without `MAJOR-JUSTIFICATION: <which trigger>` in the PR
  body and explicit owner approval in the session. Inside 30 days it needs both tokens.
- When a review bot's finding conflicts with the written policy, re-read the policy's own exceptions
  before changing anything, then fix the policy or the PR. Never escalate the version to satisfy the
  finding.

## Key Files

- Solution: `NextUnit.slnx`
- Roadmap: `PLANS.md`
- Versions: `Directory.Build.props`, `Directory.Packages.props`
