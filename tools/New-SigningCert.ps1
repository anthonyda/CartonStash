<#
.SYNOPSIS
    Creates CartonStash's self-signed code-signing certificate (spec 007 T1b). Run once.

.DESCRIPTION
    - Creates a code-signing certificate "CN=Anthony" (3 years) in the current user's certificate store.
    - Exports the private key as a password-protected PFX to %USERPROFILE%\.cartonstash\ (outside the repo, never commit it),
      with a random password saved next to it, readable only by you.
    - Exports the public certificate to tools\CartonStash.cer (safe to commit; it ships with every release, 007 R1.5).

    Afterwards, for GitHub Actions, store the two secrets yourself (they are credentials):
        gh secret set SIGNING_PFX_BASE64   < "$env:USERPROFILE\.cartonstash\CartonStash.pfx.base64"
        gh secret set SIGNING_PFX_PASSWORD < "$env:USERPROFILE\.cartonstash\CartonStash.pfx.password"

    The subject must stay equal to Publisher in src\CartonStash.App\Package.appxmanifest.
#>
[CmdletBinding()]
param(
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

# Random password, never printed.
$bytes = [byte[]]::new(32)
[Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$plain = [Convert]::ToBase64String($bytes)
$password = ConvertTo-SecureString $plain -AsPlainText -Force

Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $password | Out-Null
Set-Content (Join-Path $KeyFolder 'CartonStash.pfx.password') $plain -NoNewline
Set-Content (Join-Path $KeyFolder 'CartonStash.pfx.base64') ([Convert]::ToBase64String([IO.File]::ReadAllBytes($pfx))) -NoNewline
Export-Certificate -Cert $cert -FilePath $PublicCert | Out-Null

# Only the current user may read the key folder.
icacls $KeyFolder /inheritance:r /grant:r "${env:USERNAME}:(OI)(CI)F" | Out-Null

"Certificate:  $($cert.Subject), thumbprint $($cert.Thumbprint), valid until $($cert.NotAfter.ToString('yyyy-MM-dd'))"
"Private key:  $pfx (password in CartonStash.pfx.password, same folder)"
"Public cert:  $PublicCert"
