# Changelog

What's new in each version of CartonStash. Get it from the [Microsoft Store](https://apps.microsoft.com/detail/9MT37M7DBZRK).

## 1.2.0

### Fixed

- **Results say what really happened.** Three kinds of results used to mislead:
  - An update that failed before any installer ran said "The installer failed with error code 0." It now shows WinGet's own error code, with Windows' description of it when there is one. When a program WinGet needed is missing, as with a damaged EA app install, it says so and suggests repairing or reinstalling the package.
  - An update that WinGet reported as done, while Windows still lists the old version, said "Updated successfully" and came back on the next check. Autodesk Fusion does this, because it updates itself when it starts. The app now shows a warning, "Update not confirmed", instead.
  - A package whose publisher doesn't allow updating in place, such as Miniconda3, failed with "Check your internet connection." The app now says it can't be updated in place, and that you can uninstall it and install it again. It no longer offers Retry, which would fail the same way.

## 1.1.2

### Fixed

- **CartonStash says when a program is running and blocks its update.** Updating a package while it was open, for example Claude Code with `claude` running in a terminal, failed with only "The installer failed with error code 0x8A150003." The app now names the program that holds its files, for example "Claude Code is running (claude.exe), so it couldn't be updated. Close it, then press Retry." The same goes for an uninstall, and for installers that report the program as running. CartonStash never closes the program for you, and it only looks for running programs after such a failure.

## 1.1.1

Two fixes for updates that said one thing and did another.

### Fixed

- **Packages with a license agreement now update.** Updating a package such as Microsoft Power BI Desktop failed at once with "The package's license agreement wasn't accepted." CartonStash now shows the agreement before the update, as it already did for an install, and updates once you accept. **Update all** asks for every package that has one before anything starts, and leaves out the ones you decline.
- **Updating App Installer is finished properly.** WinGet runs from App Installer, so its new version can only take over once CartonStash restarts. The app used to say "Updated successfully" while the old version stayed in the list. It now says the update finishes when CartonStash restarts and offers a **Restart CartonStash** button, which reopens the app on the same page with the new version. **Update all** runs App Installer last, so the other updates aren't held up. If another program keeps WinGet open, the app says so.

## 1.1.0

CartonStash can now tell you about updates while it's closed, and it copes much better when a source or WinGet itself is missing.

### New

- **Background update checks.** Turn them on in Settings, and Windows runs a short check every 1, 6, 12 or 24 hours, every 7 days, or once a day at a time you pick, even with CartonStash closed. When updates are waiting, a notification names up to three of them, and clicking it opens the Updates page. Checks are off by default and never install anything. They're skipped on a metered connection and while the app is running an install, update or uninstall.
- **Missed checks catch up.** If your PC was off or asleep when a check was due, it runs once when you next sign in or unlock. For this, checks add an entry named "CartonStash update check" to Windows' Startup apps. It opens no window and does nothing when no check was missed. You can turn it off there, and Settings tells you what that changes.
- **Exclude packages from update checks.** On the Updates page, exclude any package you've decided not to update. It's left out of notifications, the Updates count and **Update all**, and you can still update it by hand. Settings lists your exclusions, and you can remove them there. Excluding a package changes nothing in WinGet.
- **Check history.** Settings lists the background checks of the last 7 days: when each one ran, what started it, and whether it found updates, was skipped or failed, and why.
- **Setup check at start.** When the app opens, it checks that WinGet is installed and that each source's agreements are accepted, and offers to set up whatever is missing. Nothing is installed or accepted until you choose to.

### Changed

- CartonStash is now published on the Microsoft Store only. GitHub keeps the source and this changelog, and you can still [build it yourself](https://github.com/anthonyda/CartonStash#build-from-source). If you installed a file from GitHub, [here's how to move to the Store](https://github.com/anthonyda/CartonStash/blob/main/SECURITY.md#if-you-installed-a-file-from-github).

### Fixed

- One failing source no longer stops the others. Search, details, installs and updates keep working with the sources that respond.
- When a source is left out, Search names it and says why: it can't be reached, or it reported a problem. It no longer blames your internet connection for a source's own error. If the Microsoft Store source fails because Windows has no country set, the app says where to set one.
- On a PC without WinGet, the app no longer crashes at start. It opens, explains that it needs WinGet, part of App Installer, and offers to install it for you.
- The details pane no longer keeps sections or links from the previously selected package, and messages close once they no longer apply.

### Privacy

- The Microsoft Store source is only searched after you accept its agreements in CartonStash. Until then, the app contacts it only to fetch those agreements. You can withdraw your acceptance in Settings.
- Background checks go through WinGet, like the Updates page. Their history stays on your PC and holds no user name or file paths.

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
