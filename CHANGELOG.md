# Changelog

What's new in each version of CartonStash. Get it from the [Microsoft Store](https://apps.microsoft.com/detail/9MT37M7DBZRK).

## 1.0.6

### New

- Background update checks, optional and off by default. On a schedule you choose, Windows runs a short check, even with the app closed, and a notification tells you what's waiting. It never installs anything by itself, and you can exclude any package you don't want to hear about.
- At start, the app checks that WinGet is installed and that each source's agreements are accepted, and offers to set up what's missing.

### Changed

- CartonStash is now published on the Microsoft Store only. GitHub keeps the source and this changelog, and you can still [build it yourself](https://github.com/anthonyda/CartonStash#build-from-source). If you installed a file from GitHub, [here's how to move to the Store](https://github.com/anthonyda/CartonStash/blob/main/SECURITY.md#if-you-installed-a-file-from-github).

### Fixed

- One failing source no longer stops the others from working.
- Search names any source that's missing and says why it failed.
- On a PC without WinGet, the app no longer crashes at start: it opens and says what's missing.

### Privacy

- The Microsoft Store source is only searched after you accept its agreements. Until then, the app contacts it only to fetch those agreements. You can withdraw your acceptance in Settings.

## 1.0.5

### Improved

- Smaller downloads and less disk space. CartonStash no longer carries Windows App SDK libraries it doesn't use, and the Microsoft Store version now shares Microsoft's runtime instead of bundling its own copy. On x64, the Store package drops from 92.6 MB to 45.2 MB, and from 235 MB to 115.5 MB once installed.

The app's features haven't changed since v1.0.4.

## 1.0.4

### New

- CartonStash is now on the [Microsoft Store](https://apps.microsoft.com/detail/9MT37M7DBZRK), signed by Microsoft, with updates handled by Windows.
- Added a [privacy policy](https://github.com/anthonyda/CartonStash/blob/main/PRIVACY.md).

The app itself hasn't changed since v1.0.3.

## 1.0.3

### New

- Operation logs now end with WinGet's own record of the operation, so a log is useful even when the installer writes nothing.
- The log dialog reminds you to check a log before sharing it, because it can contain your user name, PC name and file paths.

### Fixed

- Package details now load for every managed installed program, such as PowerShell.

### Security

- Copied diagnostics no longer include tokens or passwords from a private source's address.
- WinGet's record has URL credentials, query strings and download headers removed before it's added to a log.
- Package icons load over https only.

## 1.0.2

First public release of CartonStash, a graphical interface for WinGet: search, install, update and uninstall apps, and see everything installed on your PC.

### Fixed

- The portable app no longer crashes at start.
