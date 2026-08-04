#!/usr/bin/env pwsh
# Fails when a solution resolves a NuGet package that carries a known vulnerability.
#
# With -BaselinePath the check is a diff against another checkout of the same repository: only
# vulnerabilities absent from that baseline fail. An advisory published against a package the base
# branch already carries therefore does not turn unrelated pull requests red, while a pull request
# that adds such a package, or moves a package into a vulnerable range, does fail.
#
# Without -BaselinePath the check covers everything the solution resolves. That is the standing
# scan for packages already on the base branch.
#
# Exit codes: 0 clean, 1 findings, 2 bad usage or a malformed allowlist.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Solution,

    [Parameter(Mandatory = $true)]
    [string] $Allowlist,

    [string] $BaselinePath,

    [ValidateSet('warn', 'fail')]
    [string] $ExpiredEntryAction = 'warn'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# dotnet reports failure through its exit code, and it writes ordinary NuGet warnings to stderr.
# Without this, PowerShell 7.3 and later turn any stderr line into a terminating error.
$PSNativeCommandUseErrorActionPreference = $false

$script:OnActions = $env:GITHUB_ACTIONS -eq 'true'
$script:Summary = [System.Collections.Generic.List[string]]::new()

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

function Read-AllowlistFile {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Allowlist file not found: $Path"
    }

    $pattern = '^(?<advisory>GHSA-[0-9A-Za-z]{4}-[0-9A-Za-z]{4}-[0-9A-Za-z]{4})[ \t]+(?<expires>\d{4}-\d{2}-\d{2})[ \t]+(?<reason>\S.*)$'
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
            throw "${Path}:${lineNumber}: expected '<GHSA id> <YYYY-MM-DD> <reason>', got: $trimmed"
        }

        $advisory = $match.Groups['advisory'].Value.ToLowerInvariant()
        if ($seen.ContainsKey($advisory)) {
            throw "${Path}:${lineNumber}: $($match.Groups['advisory'].Value) is already allowed on line $($seen[$advisory])."
        }
        $seen[$advisory] = $lineNumber

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

        $entries.Add([pscustomobject]@{
                # Advisory is lower cased so that matching is case insensitive; Text keeps the
                # identifier as written for anything that ends up in a message.
                Advisory = $advisory
                Text     = $match.Groups['advisory'].Value
                Expires  = $expires
                Reason   = $match.Groups['reason'].Value
                Line     = $lineNumber
            })
    }

    return $entries.ToArray()
}

function Get-SolutionVulnerability {
    param([string] $Path, [string] $Label)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Label not found: $Path"
    }

    Write-Host "Restoring $Label ($Path)"
    & dotnet restore $Path | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed for $Path with exit code $LASTEXITCODE."
    }

    Write-Host "Listing vulnerable packages for $Label"
    $output = & dotnet list $Path package --vulnerable --include-transitive --format json --output-version 1
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet list package failed for $Path with exit code $LASTEXITCODE."
    }

    $text = ($output -join [System.Environment]::NewLine)
    try {
        $report = $text | ConvertFrom-Json
    }
    catch {
        throw "dotnet list package did not return JSON for ${Path}: $text"
    }

    foreach ($problem in (Get-OptionalArray -InputObject $report -Name 'problems')) {
        if ((Get-OptionalProperty -InputObject $problem -Name 'level') -eq 'error') {
            throw "dotnet list package reported an error for ${Path}: $(Get-OptionalProperty -InputObject $problem -Name 'text')"
        }
    }

    $found = [System.Collections.Generic.List[object]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($project in (Get-OptionalArray -InputObject $report -Name 'projects')) {
        foreach ($framework in (Get-OptionalArray -InputObject $project -Name 'frameworks')) {
            $packages = @(Get-OptionalArray -InputObject $framework -Name 'topLevelPackages') +
                        @(Get-OptionalArray -InputObject $framework -Name 'transitivePackages')

            foreach ($package in $packages) {
                $id = [string](Get-OptionalProperty -InputObject $package -Name 'id')
                $version = [string](Get-OptionalProperty -InputObject $package -Name 'resolvedVersion')

                foreach ($vulnerability in (Get-OptionalArray -InputObject $package -Name 'vulnerabilities')) {
                    $url = [string](Get-OptionalProperty -InputObject $vulnerability -Name 'advisoryurl')
                    $advisory = ($url -split '/')[-1].ToLowerInvariant()

                    # The same transitive package is reported once per project that pulls it in.
                    if (-not $seen.Add("$id|$version|$advisory")) {
                        continue
                    }

                    $found.Add([pscustomobject]@{
                            Package  = $id
                            Version  = $version
                            Severity = [string](Get-OptionalProperty -InputObject $vulnerability -Name 'severity')
                            Advisory = $advisory
                            Url      = $url
                        })
                }
            }
        }
    }

    return $found.ToArray()
}

function Format-Finding {
    param([object] $Finding)

    return "- {0} {1} ({2}) {3}" -f $Finding.Package, $Finding.Version, $Finding.Severity, $Finding.Url
}

try {
    $entries = Read-AllowlistFile -Path $Allowlist
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
    [void] $allowed.Add($entry.Advisory)
}

$current = Get-SolutionVulnerability -Path $Solution -Label 'solution'

$baselineKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$hasBaseline = -not [string]::IsNullOrWhiteSpace($BaselinePath)
if ($hasBaseline) {
    # The baseline is the same repository at another revision, so the solution path is the same.
    $baseline = Get-SolutionVulnerability -Path (Join-Path $BaselinePath $Solution) -Label 'baseline'

    # Keyed without the version: moving between two versions that share an advisory makes nothing
    # worse, while leaving a clean version for a vulnerable one adds a pair the baseline lacks.
    foreach ($item in $baseline) {
        [void] $baselineKeys.Add("$($item.Package)|$($item.Advisory)")
    }
}

$suppressed = @($current | Where-Object { $allowed.Contains($_.Advisory) })
$remaining = @($current | Where-Object { -not $allowed.Contains($_.Advisory) })
$inherited = @()
$findings = $remaining
if ($hasBaseline) {
    $inherited = @($remaining | Where-Object { $baselineKeys.Contains("$($_.Package)|$($_.Advisory)") })
    $findings = @($remaining | Where-Object { -not $baselineKeys.Contains("$($_.Package)|$($_.Advisory)") })
}

Add-Summary '## Dependency vulnerability scan'
Add-Summary
if ($hasBaseline) {
    Add-Summary "Scanned ``$Solution`` and failed on what is not already in the baseline revision."
}
else {
    Add-Summary "Scanned ``$Solution`` and failed on every vulnerability that is not allowlisted."
}
Add-Summary

if ($findings.Count -gt 0) {
    Add-Summary "### Vulnerabilities this check fails on ($($findings.Count))"
    Add-Summary
    foreach ($finding in $findings) {
        Add-Summary (Format-Finding -Finding $finding)
        Write-Annotation -Level 'error' -Message (
            "$($finding.Package) $($finding.Version) has a known $($finding.Severity) severity vulnerability: $($finding.Url)")
    }
    Add-Summary
    Add-Summary ("Remove the package or move to a fixed version. If neither is possible yet, add the " +
        "advisory to ``$Allowlist`` with an expiry date and a reason.")
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
        $entry = $active | Where-Object { $_.Advisory -eq $item.Advisory } | Select-Object -First 1
        Add-Summary ("{0}, expires {1}: {2}" -f
            (Format-Finding -Finding $item), $entry.Expires.ToString('yyyy-MM-dd'), $entry.Reason)
    }
    Add-Summary
}

$unusedEntries = @()
if (-not $hasBaseline) {
    # Only meaningful for a full scan: in a diff run the current set is the head revision's whole
    # graph, but an entry may still be covering something the pull request has not touched.
    $seenAdvisories = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($item in $current) {
        [void] $seenAdvisories.Add($item.Advisory)
    }
    $unusedEntries = @($active | Where-Object { -not $seenAdvisories.Contains($_.Advisory) })
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
