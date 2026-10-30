# Security policy

## Reporting a vulnerability

Please don't open a public issue. Report it privately through
[GitHub's private vulnerability reporting](https://github.com/anthonyda/CartonStash/security/advisories/new), with the
version you're using, what you found and how to reproduce it.

Only the latest release gets security fixes.

## Checking what you download

The release certificate's thumbprint is `99EE388A0F4388D0118678B7043D8626AA904441`. Check it before you trust the
certificate, and see [Download and install](README.md#download-and-install) for the steps.

The certificate is self-signed, so it can't be revoked. If the signing key were ever compromised, the project would say
so here and in a release. You would then remove the certificate from `LocalMachine\TrustedPeople` yourself.
