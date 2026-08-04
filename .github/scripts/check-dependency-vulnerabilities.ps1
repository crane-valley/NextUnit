#!/usr/bin/env pwsh
# Fails when a project resolves a NuGet package that carries a known vulnerability.
#
# With -BaselinePath the check is a diff against another checkout of the same repository: only
# vulnerabilities absent from that baseline fail. An advisory published against a package the base
# branch already carries therefore does not turn unrelated pull requests red, while a pull request
# that adds such a package, or pulls one into a project that did not have it, does fail.
#
# Without -BaselinePath the check covers everything the targets resolve. That is the standing scan
# for packages already on the base branch.
#
# Every .csproj under -Root has to be reachable from -Target, so a project that belongs to no
# scanned solution fails the run instead of going unscanned.
#
# Exit codes: 0 clean, 1 findings, 2 bad usage, an unscanned project, or a malformed allowlist.

[CmdletBinding()]
param(
    # Solutions or projects to scan, comma separated or as repeated values. Every .csproj under
    # -Root must be covered by one of them.
    [Parameter(Mandatory = $true)]
    [string[]] $Target,

    [Parameter(Mandatory = $true)]
    [string] $Allowlist,

    [string] $Root = '.',

    [string] $BaselinePath,

    [ValidateSet('warn', 'fail')]
    [string] $ExpiredEntryAction = 'warn',

    # An exception is a short lived promise to come back to it, so a date far enough out to be
    # indistinguishable from never is rejected outright.
    [int] $MaxAllowlistDays = 90
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# dotnet reports failure through its exit code, and it writes ordinary NuGet warnings to stderr.
# Without this, PowerShell 7.3 and later turn any stderr line into a terminating error.
$PSNativeCommandUseErrorActionPreference = $false

$script:OnActions = $env:GITHUB_ACTIONS -eq 'true'
$script:Summary = [System.Collections.Generic.List[string]]::new()

# pwsh -File hands every argument through as a literal string, so a comma separated list arrives as
# one element rather than as the array PowerShell would build for the same text inline.
$targets = @($Target |
    ForEach-Object { $_ -split ',' } |
    ForEach-Object { $_.Trim() } |
    Where-Object { $_.Length -gt 0 })
if ($targets.Count -eq 0) {
    Write-Host 'No scan target was given.'
    exit 2
}

# Progress and the report go to the host on purpose. Write-Output would land in the output stream
# and mix into what the functions below return.

function Add-Summary {
    param([string] $Line = '')
    $script:Summary.Add($Line)
    Write-Host $Line
}

function Write-Annotation {
    param(
        [ValidateSet('error', 'warning', 'notice')]
        [string] $Level,
        [string] $Message
    )

    if (-not $script:OnActions) {
        return
    }

    # Workflow commands are line oriented, so a multi-line message would be truncated silently.
    Write-Host ("::{0}::{1}" -f $Level, ($Message -replace '\r?\n', ' '))
}

function Get-OptionalProperty {
    param([object] $InputObject, [string] $Name)

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-OptionalArray {
    # @($null) is a one element array holding $null, so a missing JSON property cannot be wrapped
    # with @() directly.
    param([object] $InputObject, [string] $Name)

    $value = Get-OptionalProperty -InputObject $InputObject -Name $Name
    if ($null -eq $value) {
        return @()
    }

    return @($value)
}

function ConvertTo-RepositoryPath {
    # Head and baseline live in different directories, so only a path relative to the checkout root
    # is comparable between them.
    param([string] $Path, [string] $RootPath)

    return [System.IO.Path]::GetRelativePath($RootPath, $Path).Replace('\', '/')
}

function Read-AllowlistFile {
    param([string] $Path, [int] $MaxDays)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Allowlist file not found: $Path"
    }

    $pattern = '^(?<advisory>GHSA-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4})[ \t]+' +
               '(?<package>[A-Za-z0-9._-]+)[ \t]+(?<expires>\d{4}-\d{2}-\d{2})[ \t]+(?<reason>\S.*)$'
    $today = [datetime]::UtcNow.Date
    $entries = [System.Collections.Generic.List[object]]::new()
    $seen = @{}
    $lineNumber = 0

    foreach ($line in @(Get-Content -LiteralPath $Path)) {
        $lineNumber++
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) {
            continue
        }

        $match = [regex]::Match($trimmed, $pattern)
        if (-not $match.Success) {
            throw "${Path}:${lineNumber}: expected '<GHSA id> <package id> <YYYY-MM-DD> <reason>', got: $trimmed"
        }

        $advisory = $match.Groups['advisory'].Value
        $package = $match.Groups['package'].Value
        $key = "$advisory|$package".ToLowerInvariant()
        if ($seen.ContainsKey($key)) {
            throw "${Path}:${lineNumber}: $advisory is already allowed for $package on line $($seen[$key])."
        }
        $seen[$key] = $lineNumber

        $expires = [datetime]::MinValue
        $text = $match.Groups['expires'].Value
        $parsed = [datetime]::TryParseExact(
            $text,
            'yyyy-MM-dd',
            [cultureinfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::None,
            [ref] $expires)
        if (-not $parsed) {
            throw "${Path}:${lineNumber}: '$text' is not a calendar date."
        }

        if (($expires - $today).TotalDays -gt $MaxDays) {
            throw "${Path}:${lineNumber}: $text is more than $MaxDays days away; an exception has to be short lived."
        }

        $entries.Add([pscustomobject]@{
                # Key is lower cased so that matching is case insensitive; Text keeps the entry as
                # written for anything that ends up in a message.
                Key     = $key
                Text    = "$advisory $package"
                Expires = $expires
                Reason  = $match.Groups['reason'].Value
                Line    = $lineNumber
            })
    }

    return $entries.ToArray()
}

function Get-TargetVulnerability {
    param(
        [string] $Path,
        [string] $RootPath,
        [string] $Label,
        [string] $PropertyName,
        [string] $PropertyValue,
        [switch] $AllowMissing
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        if ($AllowMissing) {
            # A target the pull request introduces does not exist in the base revision, and
            # everything it resolves is new by definition.
            Write-Host "Skipping $Label target $Path because this revision does not have it"
            return [pscustomobject]@{ Scanned = @(); Vulnerabilities = @() }
        }

        throw "$Label target not found: $Path"
    }

    $suffix = ''
    if ($PropertyName) {
        # dotnet list package takes no MSBuild properties, so the property is set in the environment,
        # which MSBuild reads as a global property for both the restore and the listing.
        $suffix = " with $PropertyName=$PropertyValue"
        Set-Item -Path "env:$PropertyName" -Value $PropertyValue
    }

    try {
        Write-Host "Restoring $Label target $Path$suffix"
        & dotnet restore $Path | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed for $Path$suffix with exit code $LASTEXITCODE."
        }

        # --no-restore matters beyond saving a second restore: the .NET 10 listing restores on its
        # own otherwise, with default properties, which would overwrite the assets file the property
        # scoped restore above just produced and silently scan the wrong graph.
        Write-Host "Listing vulnerable packages for $Label target $Path$suffix"
        $output = & dotnet list $Path package --vulnerable --include-transitive --no-restore --format json --output-version 1
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet list package failed for $Path$suffix with exit code $LASTEXITCODE."
        }
    }
    finally {
        if ($PropertyName) {
            Remove-Item -Path "env:$PropertyName" -ErrorAction SilentlyContinue
        }
    }

    $text = ($output -join [System.Environment]::NewLine)
    try {
        $report = $text | ConvertFrom-Json
    }
    catch {
        throw "dotnet list package did not return JSON for ${Path}: $text"
    }

    # Anything dotnet has to say about the scan itself is fatal, warnings included. An audit source
    # that serves no vulnerability data reports as a warning and would otherwise leave a target that
    # was never really scanned looking clean.
    foreach ($problem in (Get-OptionalArray -InputObject $report -Name 'problems')) {
        $level = [string](Get-OptionalProperty -InputObject $problem -Name 'level')
        $message = [string](Get-OptionalProperty -InputObject $problem -Name 'text')
        throw "dotnet list package reported a $level for ${Path}: $message"
    }

    $projects = @(Get-OptionalArray -InputObject $report -Name 'projects')
    if ($projects.Count -eq 0) {
        throw "dotnet list package reported no project for $Path."
    }

    $scanned = [System.Collections.Generic.List[string]]::new()
    $found = [System.Collections.Generic.List[object]]::new()

    foreach ($project in $projects) {
        $projectPath = ConvertTo-RepositoryPath -RootPath $RootPath `
            -Path ([string](Get-OptionalProperty -InputObject $project -Name 'path'))
        $scanned.Add($projectPath)

        foreach ($framework in (Get-OptionalArray -InputObject $project -Name 'frameworks')) {
            $moniker = [string](Get-OptionalProperty -InputObject $framework -Name 'framework')
            $packages = @(Get-OptionalArray -InputObject $framework -Name 'topLevelPackages') +
                        @(Get-OptionalArray -InputObject $framework -Name 'transitivePackages')

            foreach ($package in $packages) {
                $id = [string](Get-OptionalProperty -InputObject $package -Name 'id')
                $version = [string](Get-OptionalProperty -InputObject $package -Name 'resolvedVersion')

                foreach ($vulnerability in (Get-OptionalArray -InputObject $package -Name 'vulnerabilities')) {
                    $url = [string](Get-OptionalProperty -InputObject $vulnerability -Name 'advisoryurl')
                    $advisory = ($url -split '/')[-1].ToLowerInvariant()

                    $found.Add([pscustomobject]@{
                            Project   = $projectPath
                            Framework = $moniker
                            Package   = $id
                            Version   = $version
                            Severity  = [string](Get-OptionalProperty -InputObject $vulnerability -Name 'severity')
                            Advisory  = $advisory
                            Url       = $url
                            # Scoped to the project and framework, so pulling a package that some
                            # test project already carries into a shipped one still counts as new.
                            # The version is deliberately absent: moving between two versions that
                            # share one advisory makes nothing worse, and failing on it would block
                            # the very bumps that eventually clear the advisory.
                            Key       = "$projectPath|$moniker|$id|$advisory".ToLowerInvariant()
                            Exception = "$advisory|$id".ToLowerInvariant()
                        })
                }
            }
        }
    }

    return [pscustomobject]@{
        Scanned         = $scanned.ToArray()
        Vulnerabilities = $found.ToArray()
    }
}

function Get-ScanResult {
    param([string[]] $Targets, [string] $RootPath, [string] $Label, [switch] $AllowMissing)

    $scanned = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $found = [System.Collections.Generic.List[object]]::new()
    $keys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($target in $Targets) {
        # A target may pin one MSBuild property, as in path|TestFramework=XUNIT, for projects whose
        # package references sit behind a condition that the default evaluation never satisfies.
        $parts = $target -split '\|', 2
        $name = ''
        $value = ''
        if ($parts.Count -eq 2) {
            $assignment = $parts[1] -split '=', 2
            if ($assignment.Count -ne 2 -or -not $assignment[0].Trim()) {
                throw "Target '$target' has a property that is not written as Name=Value."
            }
            $name = $assignment[0].Trim()
            $value = $assignment[1].Trim()
        }

        $result = Get-TargetVulnerability -Path (Join-Path $RootPath $parts[0].Trim()) -RootPath $RootPath `
            -Label $Label -PropertyName $name -PropertyValue $value -AllowMissing:$AllowMissing

        foreach ($project in $result.Scanned) {
            [void] $scanned.Add($project)
        }

        # A project can sit in more than one target, and a transitive package is reported once per
        # project that pulls it in.
        foreach ($item in $result.Vulnerabilities) {
            if ($keys.Add($item.Key)) {
                $found.Add($item)
            }
        }
    }

    return [pscustomobject]@{
        Scanned         = $scanned
        Vulnerabilities = $found.ToArray()
    }
}

function Get-UnscannedProject {
    param([string] $RootPath, [object] $Scanned, [string] $ExcludePath)

    $unscanned = [System.Collections.Generic.List[string]]::new()

    foreach ($file in @(Get-ChildItem -LiteralPath $RootPath -Recurse -File -Force -ErrorAction SilentlyContinue)) {
        if ($file.Extension -ne '.csproj') {
            continue
        }

        $relative = ConvertTo-RepositoryPath -Path $file.FullName -RootPath $RootPath
        if ($relative -match '(^|/)(bin|obj)/') {
            continue
        }

        # On CI the baseline checkout lives inside the workspace and is not part of this revision.
        if ($ExcludePath -and $relative.StartsWith("$ExcludePath/", [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        if (-not $Scanned.Contains($relative)) {
            $unscanned.Add($relative)
        }
    }

    return $unscanned.ToArray()
}

function Format-Finding {
    param([object] $Finding)

    return "- {0} {1} ({2}) in {3} [{4}] {5}" -f
        $Finding.Package, $Finding.Version, $Finding.Severity, $Finding.Project, $Finding.Framework, $Finding.Url
}

try {
    $entries = @(Read-AllowlistFile -Path $Allowlist -MaxDays $MaxAllowlistDays)
}
catch {
    Write-Annotation -Level 'error' -Message "$($_.Exception.Message)"
    Write-Host "Allowlist error: $($_.Exception.Message)"
    exit 2
}

$today = [datetime]::UtcNow.Date
$active = @($entries | Where-Object { $_.Expires -ge $today })
$expired = @($entries | Where-Object { $_.Expires -lt $today })
$allowed = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $active) {
    [void] $allowed.Add($entry.Key)
}

$baselineKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$current = $null

# Everything from here to the baseline scan is setup rather than a verdict, so a failure in it is a
# scan error and exits 2 rather than passing for a vulnerability finding.
try {
    $rootPath = (Resolve-Path -LiteralPath $Root).Path
    $hasBaseline = -not [string]::IsNullOrWhiteSpace($BaselinePath)
    $baselineFull = ''
    $excludePath = ''
    if ($hasBaseline) {
        $baselineFull = (Resolve-Path -LiteralPath $BaselinePath).Path
        $relativeBaseline = ConvertTo-RepositoryPath -Path $baselineFull -RootPath $rootPath
        if (-not $relativeBaseline.StartsWith('..')) {
            $excludePath = $relativeBaseline
        }
    }

    $current = Get-ScanResult -Targets $targets -RootPath $rootPath -Label 'head'

    # A single returned path would arrive unrolled as a bare string, which has no Count.
    $unscanned = @(Get-UnscannedProject -RootPath $rootPath -Scanned $current.Scanned -ExcludePath $excludePath)
    if ($unscanned.Count -gt 0) {
        foreach ($project in $unscanned) {
            Write-Annotation -Level 'error' -Message "$project is not covered by the vulnerability scan; add it to a scanned solution."
            Write-Host "Unscanned project: $project"
        }
        Write-Host 'Every project has to be reachable from a scanned target, otherwise its packages go unchecked.'
        exit 2
    }

    if ($hasBaseline) {
        # A target the pull request adds is absent from the base revision, which is not an error.
        $baseline = Get-ScanResult -Targets $targets -RootPath $baselineFull -Label 'baseline' -AllowMissing
        foreach ($item in $baseline.Vulnerabilities) {
            [void] $baselineKeys.Add($item.Key)
        }
    }
}
catch {
    Write-Annotation -Level 'error' -Message "$($_.Exception.Message)"
    Write-Host "Scan error: $($_.Exception.Message)"
    exit 2
}

$suppressed = @($current.Vulnerabilities | Where-Object { $allowed.Contains($_.Exception) })
$remaining = @($current.Vulnerabilities | Where-Object { -not $allowed.Contains($_.Exception) })
$inherited = @()
$findings = $remaining
if ($hasBaseline) {
    $inherited = @($remaining | Where-Object { $baselineKeys.Contains($_.Key) })
    $findings = @($remaining | Where-Object { -not $baselineKeys.Contains($_.Key) })
}

$targetList = ($targets -join ', ')

Add-Summary '## Dependency vulnerability scan'
Add-Summary
if ($hasBaseline) {
    Add-Summary "Scanned $targetList and failed on what is not already in the baseline revision."
}
else {
    Add-Summary "Scanned $targetList and failed on every vulnerability that is not allowlisted."
}
Add-Summary

if ($findings.Count -gt 0) {
    Add-Summary "### Vulnerabilities this check fails on ($($findings.Count))"
    Add-Summary
    foreach ($finding in $findings) {
        Add-Summary (Format-Finding -Finding $finding)
        Write-Annotation -Level 'error' -Message (
            "$($finding.Package) $($finding.Version) in $($finding.Project) has a known $($finding.Severity) severity vulnerability: $($finding.Url)")
    }
    Add-Summary
    Add-Summary ("Remove the package or move to a fixed version. If neither is possible yet, add the " +
        "advisory and the package to $Allowlist with an expiry date and a reason.")
    Add-Summary
}
else {
    Add-Summary 'No vulnerability that this check fails on.'
    Add-Summary
}

if ($inherited.Count -gt 0) {
    Add-Summary "### Already present in the baseline ($($inherited.Count))"
    Add-Summary
    Add-Summary 'Reported for awareness. These do not fail the pull request; the nightly scan fails on them.'
    Add-Summary
    foreach ($item in $inherited) {
        Add-Summary (Format-Finding -Finding $item)
    }
    Add-Summary
}

if ($suppressed.Count -gt 0) {
    Add-Summary "### Allowlisted ($($suppressed.Count))"
    Add-Summary
    foreach ($item in $suppressed) {
        $entry = $active | Where-Object { $_.Key -eq $item.Exception } | Select-Object -First 1
        Add-Summary ("{0}, expires {1}: {2}" -f
            (Format-Finding -Finding $item), $entry.Expires.ToString('yyyy-MM-dd'), $entry.Reason)
    }
    Add-Summary
}

$unusedEntries = @()
if (-not $hasBaseline) {
    # Only meaningful for a full scan, where the current set is everything the repository resolves.
    $seenExceptions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($item in $current.Vulnerabilities) {
        [void] $seenExceptions.Add($item.Exception)
    }
    $unusedEntries = @($active | Where-Object { -not $seenExceptions.Contains($_.Key) })
}

if ($unusedEntries.Count -gt 0) {
    Add-Summary "### Allowlist entries that match nothing ($($unusedEntries.Count))"
    Add-Summary
    foreach ($entry in $unusedEntries) {
        Add-Summary ("- {0} on line {1} covers no resolved package and can be deleted." -f $entry.Text, $entry.Line)
        Write-Annotation -Level 'notice' -Message "Allowlist entry $($entry.Text) covers no resolved package and can be deleted."
    }
    Add-Summary
}

if ($expired.Count -gt 0) {
    Add-Summary "### Expired allowlist entries ($($expired.Count))"
    Add-Summary
    foreach ($entry in $expired) {
        $message = "Allowlist entry $($entry.Text) expired on $($entry.Expires.ToString('yyyy-MM-dd')) and no longer suppresses anything: $($entry.Reason)"
        Add-Summary "- $message"
        Write-Annotation -Level $(if ($ExpiredEntryAction -eq 'fail') { 'error' } else { 'warning' }) -Message $message
    }
    Add-Summary
}

if ($script:OnActions -and $env:GITHUB_STEP_SUMMARY) {
    Set-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $script:Summary -Encoding utf8
}

if ($findings.Count -gt 0) {
    exit 1
}

if ($expired.Count -gt 0 -and $ExpiredEntryAction -eq 'fail') {
    exit 1
}

exit 0
