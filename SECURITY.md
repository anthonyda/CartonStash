# Security policy

## Reporting a vulnerability

Please don't open a public issue. Report it privately through
[GitHub's private vulnerability reporting](https://github.com/anthonyda/CartonStash/security/advisories/new), with the
version you're using, what you found and how to reproduce it.

Only the latest release gets security fixes.

## Where to get CartonStash

The only published build is the one on the [Microsoft Store](https://apps.microsoft.com/detail/9MT37M7DBZRK), which
Microsoft signs. GitHub holds the source and the changelog, with no files to install. A CartonStash download offered
anywhere else doesn't come from this project.

## If you installed a file from GitHub

Up to v1.0.5, GitHub Releases offered an MSIX and a portable zip, signed with the project's own certificate. Its
thumbprint is `99EE388A0F4388D0118678B7043D8626AA904441`. v1.0.5 was the last release signed with it, so any newer file
signed with this certificate isn't ours.

Those installs keep working but get no more updates. To move to the Store:

1. Uninstall CartonStash from Settings → Apps. If you used the portable zip, delete its folder instead.
2. Install it from the [Microsoft Store](https://apps.microsoft.com/detail/9MT37M7DBZRK). Your settings and logs don't
   carry over.
3. If you trusted the certificate, remove it. Run this in PowerShell as administrator:

   ```powershell
   Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Thumbprint -eq 99EE388A0F4388D0118678B7043D8626AA904441 | Remove-Item
   ```

The certificate is self-signed, so the project can't revoke it. Removing it is how you end that trust. If the signing
key were ever compromised, the project would say so here and in a release.
