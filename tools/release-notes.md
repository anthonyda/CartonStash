## Install

CartonStash is signed with its own certificate (not yet one Windows already trusts), so trust it once:

1. Download `CartonStash.cer` and the `.msix` for your PC (`x64` for most PCs, `arm64` for Arm devices).
2. Open PowerShell **as administrator** in the download folder and run:
   ```
   Import-Certificate -FilePath .\CartonStash.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```
3. Double-click the `.msix` and choose **Install**.

Updates install the same way, over the top (step 3 only).

**Portable:** prefer not to install? Unzip `CartonStash_*_portable.zip` anywhere and run `CartonStash.exe`.

Check downloads with `SHA256SUMS.txt` (`Get-FileHash <file>` in PowerShell).
