<#
.SYNOPSIS
    Builds CartonStash's release files (spec 007): signed MSIX + portable zip per architecture, the public
    certificate and SHA-256 checksums. Used by the release workflow and for local test releases.

.PARAMETER Version
    Release version, e.g. 1.2.3 or v1.2.3 (from the git tag, 007 R3.2). Becomes the MSIX version 1.2.3.0.

.PARAMETER PfxPath / PfxPassword
    Signing key (CI: from the SIGNING_PFX_* secrets). Without them, the certificate in the current user's store whose
    subject matches the manifest Publisher is used (local builds after tools\New-SigningCert.ps1).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [string[]]$Runtimes = @('win-x64', 'win-arm64'),
    [string]$Output = (Join-Path $PSScriptRoot '..\artifacts'),
    [string]$PfxPath,
    [string]$PfxPassword
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

$signtool = Get-ChildItem (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.buildtools') -Recurse -Filter signtool.exe |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signtool) { throw 'signtool.exe not found; run dotnet restore first.' }

# The manifest version follows the tag (R3.2); restored afterwards so the working tree stays clean.
$manifest = Get-Content $manifestPath -Raw
$publisher = ([xml]$manifest).Package.Identity.Publisher
try {
    Set-Content $manifestPath ($manifest -replace '(<Identity [^>]*Version=")[^"]+(")', "`${1}$msixVersion`$2") -NoNewline

    foreach ($rid in $Runtimes) {
        $arch = $rid.Split('-')[1]

        # MSIX (unsigned from the build, signed below).
        $packages = Join-Path $app 'AppPackages'
        if (Test-Path $packages) { Remove-Item $packages -Recurse -Force }
        dotnet publish $app -c Release -r $rid -p:PackageMsix=true -p:Version=$semver -p:Platform=$arch
        if ($LASTEXITCODE) { throw "MSIX build failed for $rid." }
        $msix = Get-ChildItem $packages -Recurse -Filter *.msix | Select-Object -First 1
        $msixOut = Join-Path $Output "CartonStash_${semver}_$arch.msix"
        Copy-Item $msix.FullName $msixOut

        $sign = @('sign', '/fd', 'SHA256')
        $sign += if ($PfxPath) { @('/f', $PfxPath, '/p', $PfxPassword) } else {
            $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object Subject -eq $publisher |
                Sort-Object NotAfter -Descending | Select-Object -First 1
            if (-not $cert) { throw "No code-signing certificate '$publisher' in the current user's store. Run tools\New-SigningCert.ps1." }
            @('/sha1', $cert.Thumbprint, '/s', 'My')
        }
        & $signtool.FullName @sign $msixOut
        if ($LASTEXITCODE) { throw "Signing failed for $msixOut." }

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
Get-ChildItem $Output -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name |
    ForEach-Object { "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } |
    Set-Content (Join-Path $Output 'SHA256SUMS.txt')

Get-ChildItem $Output | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
