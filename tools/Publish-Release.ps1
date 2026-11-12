<#
.SYNOPSIS
    Builds CartonStash's release files (spec 007) per architecture: the Microsoft Store MSIX and a portable zip.
    Used by the release workflow and for local builds. Nothing here is signed: Microsoft signs the Store MSIX after
    certification (R6.3), and the portable zip is for local use only (R9.1, R9.6).

.PARAMETER Version
    Release version, e.g. 1.2.3 or v1.2.3 (from the git tag, 007 R3.2). Becomes the MSIX version 1.2.3.0.

.PARAMETER Force
    Empties -Output and -StoreOutput even when they hold files that aren't build outputs. Without it, only the default
    folders (artifacts\, store\) or folders holding build files alone are emptied (bug review BR-31).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string[]]$Runtimes = @('win-x64', 'win-arm64'),
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts'),
    [string]$StoreOutput = (Join-Path $PSScriptRoot '..\store'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$app = Join-Path $root 'src\CartonStash.App'
$manifestPath = Join-Path $app 'Package.appxmanifest'

$semver = $Version.TrimStart('v')
if ($semver -notmatch '^(\d+)\.(\d+)\.(\d+)$') { throw "Version must look like 1.2.3 (got '$Version')." }
$msixVersion = "$semver.0"

# BR-31: each build starts with empty output folders, but a mistyped path is never emptied without asking.
$buildFiles = '*.zip', '*.msix', '*.msixupload', '*.appxsym', '*.msixbundle'
$defaults = (Join-Path $root 'artifacts'), (Join-Path $root 'store')
foreach ($folder in $Output, $StoreOutput) {
    New-Item -ItemType Directory -Force $folder | Out-Null
    $files = @(Get-ChildItem $folder -File)
    $other = @($files | Where-Object { $name = $_.Name; -not ($buildFiles | Where-Object { $name -like $_ }) })
    $isDefault = (Resolve-Path $folder).Path.TrimEnd('\') -in $defaults
    if ($other.Count -gt 0 -and -not $isDefault -and -not $Force) {
        throw "$folder holds files that aren't build outputs ($(($other | Select-Object -First 3 -ExpandProperty Name) -join ', ')). Use an empty folder or pass -Force."
    }
    $files | Remove-Item
}
$Output = (Resolve-Path $Output).Path
$StoreOutput = (Resolve-Path $StoreOutput).Path

# Each build starts clean. An MSIX leaves an empty resource list, because the shared runtime supplies WinUI's resources,
# and a portable build straight after it would reuse that list and crash at start (R1.6).
function Clear-Build {
    foreach ($folder in 'bin\Release', 'obj\Release') {
        $path = Join-Path $app $folder
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }
}

function Build-Msix([string]$Rid, [string]$Arch, [string]$Destination) {
    Clear-Build
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

        # MSIX for the Store (R6.1).
        Set-Content $manifestPath $store -NoNewline
        Build-Msix $rid $arch (Join-Path $StoreOutput "CartonStash_${semver}_$arch.msix")
        Set-Content $manifestPath $manifest -NoNewline

        # Portable zip: the unpackaged, self-contained app folder (R1.4). EnableMsixTooling makes publish include
        # CartonStash.pri, the app's compiled XAML; without it the app crashes at start (R1.6).
        $portable = Join-Path ([IO.Path]::GetTempPath()) "cartonstash-portable-$arch"
        if (Test-Path $portable) { Remove-Item $portable -Recurse -Force }
        Clear-Build
        dotnet publish $app -c Release -r $rid --self-contained true -p:EnableMsixTooling=true -p:Version=$semver -p:Platform=$arch -o $portable
        if ($LASTEXITCODE) { throw "Portable build failed for $rid." }
        $pri = Join-Path $portable 'CartonStash.pri'
        if (-not (Test-Path $pri)) { throw "Portable build for $rid has no CartonStash.pri: the app would crash at start." }
        # A complete file holds WinUI's resources too, about 2.4 MB. Without them the app crashes the same way.
        if ((Get-Item $pri).Length -lt 1MB) { throw "Portable build for $rid has an incomplete CartonStash.pri: the app would crash at start." }
        Get-ChildItem $portable -Filter *.pdb | Remove-Item
        Compress-Archive -Path (Join-Path $portable '*') -DestinationPath (Join-Path $Output "CartonStash_${semver}_${arch}_portable.zip")
        Remove-Item $portable -Recurse -Force
    }
}
finally {
    Set-Content $manifestPath $manifest -NoNewline
}

Get-ChildItem $Output, $StoreOutput | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
