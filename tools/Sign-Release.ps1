<#
.SYNOPSIS
    Signs CartonStash's release files (spec 007 R5): each MSIX and the first-party binaries in each portable zip, every
    signature timestamped, then writes SHA-256 checksums. Run after tools\Publish-Release.ps1.

.PARAMETER PfxPath / PfxPassword
    Signing key (CI: from the release environment's secrets).

.PARAMETER Certificate
    The public certificate every signature must match. Only a test key needs anything but tools\CartonStash.cer.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PfxPath,
    [Parameter(Mandatory)][string]$PfxPassword,
    [string]$Folder = (Join-Path $PSScriptRoot '..\artifacts'),
    [string]$Certificate = (Join-Path $PSScriptRoot 'CartonStash.cer')
)

$ErrorActionPreference = 'Stop'
$Folder = (Resolve-Path $Folder).Path
$firstParty = 'CartonStash.exe', 'CartonStash.dll', 'CartonStash.Core.dll' # R5.6: the exe alone is only a launcher
$thumbprint = (Get-PfxCertificate $Certificate).Thumbprint

# From the Windows SDK, not from NuGet: no package code runs near the key (R5.1).
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1
if (-not $signtool) { throw 'signtool.exe not found; install the Windows SDK.' }

function Sign([string[]]$Files) {
    & $signtool.FullName sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f $PfxPath /p $PfxPassword @Files
    if ($LASTEXITCODE) { throw "Signing failed for $($Files -join ', ')." }

    # R5.5. The thumbprint, not trust: the certificate isn't trusted on the runner.
    foreach ($file in $Files) {
        $signature = Get-AuthenticodeSignature $file
        if ($signature.SignerCertificate.Thumbprint -ne $thumbprint) { throw "$file is not signed with certificate $thumbprint." }
        if (-not $signature.TimeStamperCertificate) { throw "$file has no timestamp." }
    }
}

$msix = @(Get-ChildItem $Folder -Filter *.msix)
$zips = @(Get-ChildItem $Folder -Filter *_portable.zip)
if (-not $msix -or -not $zips) { throw "No release files in $Folder. Run tools\Publish-Release.ps1 first." }

Sign $msix.FullName

foreach ($zip in $zips) {
    $work = Join-Path ([IO.Path]::GetTempPath()) "cartonstash-sign-$($zip.BaseName)"
    if (Test-Path $work) { Remove-Item $work -Recurse -Force }
    Expand-Archive $zip.FullName $work
    Sign ($firstParty | ForEach-Object { Join-Path $work $_ })
    Remove-Item $zip.FullName
    Compress-Archive -Path (Join-Path $work '*') -DestinationPath $zip.FullName
    Remove-Item $work -Recurse -Force
}

Get-ChildItem $Folder -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name |
    ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } |
    Set-Content (Join-Path $Folder 'SHA256SUMS.txt')

Get-ChildItem $Folder | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
