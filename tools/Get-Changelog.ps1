<#
.SYNOPSIS
    Prints one version's section of CHANGELOG.md, for its GitHub Release notes (spec 007 R9.3). Fails if the section
    is missing or empty, so a release can't go out without its notes.

.PARAMETER Version
    Release version, e.g. 1.2.3 or v1.2.3 (from the git tag).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$Path = (Join-Path $PSScriptRoot '..\CHANGELOG.md')
)

$ErrorActionPreference = 'Stop'

$semver = $Version.TrimStart('v')
$lines = @(Get-Content $Path)
$start = [array]::IndexOf($lines, "## $semver")
if ($start -lt 0) { throw "CHANGELOG.md has no '## $semver' section." }

$section = for ($i = $start + 1; $i -lt $lines.Count -and $lines[$i] -notmatch '^## '; $i++) { $lines[$i] }
$notes = ($section -join "`n").Trim()
if (-not $notes) { throw "CHANGELOG.md's '## $semver' section is empty." }

$notes
