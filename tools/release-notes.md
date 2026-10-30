## Install

CartonStash is signed with its own certificate (not yet one Windows already trusts), so trust it once:

1. Download `CartonStash.cer` and the `.msix` for your PC (`x64` for most PCs, `arm64` for Arm devices).
2. Open PowerShell **as administrator** in the download folder and check the certificate. This must print `99EE388A0F4388D0118678B7043D8626AA904441`:
   ```
   (Get-PfxCertificate .\CartonStash.cer).Thumbprint
   ```
3. Trust it:
   ```
   Import-Certificate -FilePath .\CartonStash.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```
4. Double-click the `.msix` and choose **Install**.

Updates install the same way, over the top (step 4 only).

**Portable:** prefer not to install? Unzip `CartonStash_*_portable.zip` anywhere and run `CartonStash.exe`. Its main binaries are signed with the same certificate, but Windows still shows an unknown publisher.

**Check a download:** compare `Get-FileHash <file>` with `SHA256SUMS.txt`, or confirm it was built by this repository's release workflow:
```
gh attestation verify <file> --repo anthonyda/CartonStash
```
