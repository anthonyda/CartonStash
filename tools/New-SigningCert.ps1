<#
.SYNOPSIS
    Creates CartonStash's self-signed code-signing certificate (spec 007 T1b). Only needed to replace the key: users then
    have to trust the new certificate, and the MSIX stops updating over the old one until they do.

.DESCRIPTION
    - Creates a code-signing certificate "CN=Anthony" (3 years).
    - Exports the private key as a PFX to %USERPROFILE%\.cartonstash\ (outside the repo, never commit it), protected by the
      password you give it. Let your password manager generate one.
    - Removes the key from the certificate store, so the PFX is the only copy (R5.11).
    - Exports the public certificate to tools\CartonStash.cer (safe to commit; it ships with every release, 007 R1.5).

    Afterwards, store the two secrets in the release environment yourself (they are credentials):
        [Convert]::ToBase64String([IO.File]::ReadAllBytes("$env:USERPROFILE\.cartonstash\CartonStash.pfx")) | gh secret set SIGNING_PFX_BASE64 --env release
        gh secret set SIGNING_PFX_PASSWORD --env release
    Then save the PFX and its password in your password manager and delete %USERPROFILE%\.cartonstash\.

    The subject must stay equal to Publisher in src\CartonStash.App\Package.appxmanifest.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][securestring]$Password,
    [string]$Subject = 'CN=Anthony',
    [string]$KeyFolder = (Join-Path $env:USERPROFILE '.cartonstash'),
    [string]$PublicCert = (Join-Path $PSScriptRoot 'CartonStash.cer'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$pfx = Join-Path $KeyFolder 'CartonStash.pfx'
if ((Test-Path $pfx) -and -not $Force) {
    throw "$pfx already exists. Use -Force to replace it (installed builds signed with the old certificate keep working only while it is trusted)."
}

New-Item -ItemType Directory -Force $KeyFolder | Out-Null

$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -FriendlyName 'CartonStash code signing' `
    -KeyUsage DigitalSignature `
    -KeyAlgorithm RSA -KeyLength 3072 `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -NotAfter (Get-Date).AddYears(3) `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')

try {
    Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $Password | Out-Null
    Export-Certificate -Cert $cert -FilePath $PublicCert | Out-Null
}
finally {
    Remove-Item (Join-Path 'Cert:\CurrentUser\My' $cert.Thumbprint) -DeleteKey
}

# Only the current user may read the key folder.
icacls $KeyFolder /inheritance:r /grant:r "${env:USERNAME}:(OI)(CI)F" | Out-Null

"Certificate:  $($cert.Subject), thumbprint $($cert.Thumbprint), valid until $($cert.NotAfter.ToString('yyyy-MM-dd'))"
"Private key:  $pfx"
"Public cert:  $PublicCert"
