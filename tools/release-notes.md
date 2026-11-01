> **Updating the MSIX from v1.0.5 or earlier?** It now needs the Windows App SDK runtime, version 2.6.4. Install it first (step 1 below). Without it, the update stops with "App Installer failed to install package dependencies". The Store version and portable zip need nothing new.

## Install

The easiest way is the [Microsoft Store](https://apps.microsoft.com/detail/9MT37M7DBZRK). To use the files here, install the runtime, then trust CartonStash's certificate once, because it isn't yet one Windows already trusts:

1. Install the Windows App SDK runtime 2.6.4, if you don't have it: [x64](https://aka.ms/windowsappsdk/2.6/2.6.4/windowsappruntimeinstall-x64.exe) or [Arm64](https://aka.ms/windowsappsdk/2.6/2.6.4/windowsappruntimeinstall-arm64.exe), from [Microsoft's downloads page](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads).
2. Download `CartonStash.cer` and the `.msix` for your PC (`x64` for most PCs, `arm64` for Arm devices).
3. Open PowerShell **as administrator** in the download folder and check the certificate. This must print `99EE388A0F4388D0118678B7043D8626AA904441`:
   ```
   (Get-PfxCertificate .\CartonStash.cer).Thumbprint
   ```
4. Trust it:
   ```
   Import-Certificate -FilePath .\CartonStash.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```
5. Double-click the `.msix` and choose **Install**.

Updates install the same way, over the top (step 5 only).

**Portable:** prefer not to install? Unzip `CartonStash_*_portable.zip` anywhere and run `CartonStash.exe`. It needs no runtime or certificate. Its main binaries are signed with the same certificate, but Windows still shows an unknown publisher.

**Check a download:** compare `Get-FileHash <file>` with `SHA256SUMS.txt`, or confirm it was built by this repository's release workflow:
```
gh attestation verify <file> --repo anthonyda/CartonStash
```
