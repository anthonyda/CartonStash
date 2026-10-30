<#
.SYNOPSIS
    Builds CartonStash's release files (spec 007), unsigned: MSIX + portable zip per architecture and the public
    certificate. Used by the release workflow and for local test releases. Signing is a separate step, so the
    key is never around while build code runs (R5.1): see tools\Sign-Release.ps1.

.PARAMETER Version
    Release version, e.g. 1.2.3 or v1.2.3 (from the git tag, 007 R3.2). Becomes the MSIX version 1.2.3.0.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string[]]$Runtimes = @('win-x64', 'win-arm64'),
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$app = Join-Path $root 'src\CartonStash.App'
$manifestPath = Join-Path $app 'Package.appxmanifest'

$semver = $Version.TrimStart('v')
if ($semver -notmatch '^(\d+)\.(\d+)\.(\d+)$') { throw "Version must look like 1.2.3 (got '$Version')." }
$msixVersion = "$semver.0"

New-Item -ItemType Directory -Force $Output | Out-Null
$Output = (Resolve-Path $Output).Path
Get-ChildItem $Output -File | Remove-Item

# The manifest version follows the tag (R3.2); restored afterwards so the working tree stays clean.
$manifest = Get-Content $manifestPath -Raw
try {
    Set-Content $manifestPath ($manifest -replace '(<Identity [^>]*Version=")[^"]+(")', "`${1}$msixVersion`$2") -NoNewline

    foreach ($rid in $Runtimes) {
        $arch = $rid.Split('-')[1]

        # MSIX.
        $packages = Join-Path $app 'AppPackages'
        if (Test-Path $packages) { Remove-Item $packages -Recurse -Force }
        dotnet publish $app -c Release -r $rid -p:PackageMsix=true -p:Version=$semver -p:Platform=$arch
        if ($LASTEXITCODE) { throw "MSIX build failed for $rid." }
        $msix = Get-ChildItem $packages -Recurse -Filter *.msix | Select-Object -First 1
        Copy-Item $msix.FullName (Join-Path $Output "CartonStash_${semver}_$arch.msix")

        # Portable zip: the unpackaged, self-contained app folder (R1.4).
        $portable = Join-Path ([IO.Path]::GetTempPath()) "cartonstash-portable-$arch"
        if (Test-Path $portable) { Remove-Item $portable -Recurse -Force }
        dotnet publish $app -c Release -r $rid --self-contained true -p:Version=$semver -p:Platform=$arch -o $portable
        if ($LASTEXITCODE) { throw "Portable build failed for $rid." }
        Get-ChildItem $portable -Filter *.pdb | Remove-Item
        Compress-Archive -Path (Join-Path $portable '*') -DestinationPath (Join-Path $Output "CartonStash_${semver}_${arch}_portable.zip")
        Remove-Item $portable -Recurse -Force
    }
}
finally {
    Set-Content $manifestPath $manifest -NoNewline
}

Copy-Item (Join-Path $PSScriptRoot 'CartonStash.cer') $Output

Get-ChildItem $Output | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
