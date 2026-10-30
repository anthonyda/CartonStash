<#
.SYNOPSIS
    Builds CartonStash's release files (spec 007), unsigned: MSIX + portable zip per architecture and the public
    certificate. Used by the release workflow and for local test releases. Signing is a separate step, so the
    key is never around while build code runs (R5.1): see tools\Sign-Release.ps1.
    The Microsoft Store MSIX goes to its own folder, because Microsoft signs those files, not the project (R6.3).

.PARAMETER Version
    Release version, e.g. 1.2.3 or v1.2.3 (from the git tag, 007 R3.2). Becomes the MSIX version 1.2.3.0.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string[]]$Runtimes = @('win-x64', 'win-arm64'),
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts'),
    [string]$StoreOutput = (Join-Path $PSScriptRoot '..\store')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$app = Join-Path $root 'src\CartonStash.App'
$manifestPath = Join-Path $app 'Package.appxmanifest'

$semver = $Version.TrimStart('v')
if ($semver -notmatch '^(\d+)\.(\d+)\.(\d+)$') { throw "Version must look like 1.2.3 (got '$Version')." }
$msixVersion = "$semver.0"

foreach ($folder in $Output, $StoreOutput) {
    New-Item -ItemType Directory -Force $folder | Out-Null
    Get-ChildItem $folder -File | Remove-Item
}
$Output = (Resolve-Path $Output).Path
$StoreOutput = (Resolve-Path $StoreOutput).Path

function Build-Msix([string]$Rid, [string]$Arch, [string]$Destination) {
    $packages = Join-Path $app 'AppPackages'
    if (Test-Path $packages) { Remove-Item $packages -Recurse -Force }
    dotnet publish $app -c Release -r $Rid -p:PackageMsix=true -p:Version=$semver -p:Platform=$Arch
    if ($LASTEXITCODE) { throw "MSIX build failed for $Rid." }
    $msix = Get-ChildItem $packages -Recurse -Filter *.msix | Select-Object -First 1
    Copy-Item $msix.FullName $Destination
}

# The manifest version follows the tag (R3.2); restored afterwards so the working tree stays clean.
$manifest = Get-Content $manifestPath -Raw
$versioned = $manifest -replace '(<Identity [^>]*Version=")[^"]+(")', "`${1}$msixVersion`$2"

# The identity Partner Center assigned (R6.2). Public: every Store install carries these values.
$store = $versioned `
    -replace '(<Identity [^>]*Name=")[^"]+(")', '${1}51376anthonydapps.CartonStash$2' `
    -replace '(<Identity [^>]*Publisher=")[^"]+(")', '${1}CN=8E7BFF24-3095-4F83-84B3-2D4A3E780750$2' `
    -replace '(<PublisherDisplayName>)[^<]+', '${1}anthonydapps'

try {
    foreach ($rid in $Runtimes) {
        $arch = $rid.Split('-')[1]

        # MSIX for GitHub.
        Set-Content $manifestPath $versioned -NoNewline
        Build-Msix $rid $arch (Join-Path $Output "CartonStash_${semver}_$arch.msix")

        # MSIX for the Store (R6.1).
        Set-Content $manifestPath $store -NoNewline
        Build-Msix $rid $arch (Join-Path $StoreOutput "CartonStash_${semver}_$arch.msix")
        Set-Content $manifestPath $manifest -NoNewline

        # Portable zip: the unpackaged, self-contained app folder (R1.4). EnableMsixTooling makes publish include
        # CartonStash.pri, the app's compiled XAML; without it the app crashes at start (R1.6).
        $portable = Join-Path ([IO.Path]::GetTempPath()) "cartonstash-portable-$arch"
        if (Test-Path $portable) { Remove-Item $portable -Recurse -Force }
        dotnet publish $app -c Release -r $rid --self-contained true -p:EnableMsixTooling=true -p:Version=$semver -p:Platform=$arch -o $portable
        if ($LASTEXITCODE) { throw "Portable build failed for $rid." }
        if (-not (Test-Path (Join-Path $portable 'CartonStash.pri'))) { throw "Portable build for $rid has no CartonStash.pri: the app would crash at start." }
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
