# Changelog

What's new in each version of CartonStash. Get it from the [Microsoft Store](https://apps.microsoft.com/detail/9MT37M7DBZRK).

## 1.5.1

(1.5.0 was never released.)

### New

- **Says when Windows is waiting for your permission.** Some installers ask Windows for administrator rights, and Windows' permission prompt often opens behind the app, with only a flashing shield in the taskbar. CartonStash used to keep reading "Updating…" while the queue waited. Now a banner at the top of the window says that Windows is waiting for your permission, and the operation reads "Waiting for your permission…". If the prompt is still open after a minute, a Windows notification says so too. Declining the prompt now ends the operation as **Cancelled**, "Windows' permission prompt was declined.", instead of "The installer failed with error code 1602."

## 1.4.2

A round of fixes from a review of the whole app, and a few changes. (1.4.1 was never released.)

### New

- **Retry as administrator.** Some installers need administrator rights and don't ask for them, such as Microsoft 365 Copilot's: its update used to fail with only an error code. The result now says the installer needs administrator rights, and offers **Retry as administrator**. Press it, accept Windows' permission prompt, and the update runs again in the same window. CartonStash itself keeps running without administrator rights.

### Changed

- **Update all starts at once.** It no longer asks "Update all?" first. Updates that are waiting can still be cancelled in the Activity pane until their installer starts, and license agreements are still shown.
- **Explicit sources work as in `winget`.** A source marked explicit, such as `winget-font`, is no longer used everywhere. In Settings, each explicit source has a **Use in searches** box that adds it to Search, and a short paragraph explains what explicit sources are. The Installed and Updates lists and the background checks leave them out, as `winget list` and `winget upgrade` do.

### Fixed

- **Install and Update buttons no longer go missing.** After installing a package, uninstalling it and searching for it again, its row had no Install button. A package whose update ended as "Update not confirmed" stayed on the Updates page with no Update button, and **Update all** skipped it.
- **Going back says when it didn't work.** Going back from the newest version to an older one reported "Installed successfully" even when the installer changed nothing. It now says "Not confirmed", and doesn't leave the package out of update checks.
- **The details pane follows a new search.** After searching again, the open details pane stayed tied to the old result: pressing Install there showed no progress. It now follows the same package in the new results, or closes.
- **Icons stay.** Search results lost their icons after an install or a refresh, and an icon that couldn't be downloaded left an empty space. The generic icon now shows instead.
- **Background checks are recorded.** When the PC woke up after a missed daily check, two checks could start at once and the first was cut off: no history entry and no notification. A check is also no longer dropped for coming a little early after a late one.
- **The same package can't be queued twice.** An update started on the Updates page now shows on the Search page too, which can no longer queue a second operation for it.
- **Fewer ways for the app to close by itself.** Copy when another program holds the clipboard, a failed App Installer setup, Refresh pressed twice while a source fails, release notes that can't be read, and WinGet stopping while the app is open (it now reconnects) no longer close CartonStash. An error in one operation no longer stops the operations queued after it.
- **Settings reset only once.** After a damaged settings file, the "Settings were reset" notice showed at every start and background checks kept failing. The defaults are now saved at once.
- **Check history keeps the whole error.** A failed check's reason is now kept as reported, including file paths. The privacy policy says so.
- **Smoother with big results.** Search results and long installer logs are handled without freezing the window.

## 1.4.0

### New

- **The updates count on the taskbar.** CartonStash's taskbar icon shows how many updates are waiting, the same number as on the Updates page, leaving out excluded packages. It changes as updates finish, and a background check sets it while the app is closed. With no updates, the badge goes away.
- **Find an operation in the Activity pane.** Filter the pane by package name, ID or action, and switch between **All**, **Active**, **Done** and **Failed**, each with its count. In **All**, operations are grouped under a heading for each. A **Sort** button orders them by default (the running operation, then the queued ones in the order they'll run), newest or oldest first, or by name, and the app remembers your choice.
- **The window opens the way you left it.** CartonStash remembers the window's size and position, whether it was maximized, and whether the navigation pane was open. If the window was on a screen that's no longer there, it keeps its size and Windows places it.

### Fixed

- **Pressing Tab no longer closes the app.** After a list got shorter, pressing Tab to move into it could close CartonStash at once. This happened on the Updates page after updates finished, on the Search page after a new search with fewer results, and on the Installed page after filtering. Updated packages that are still listed now keep their check mark too.

## 1.3.0

### New

- **Install for all users or just for you.** When a package has both kinds of installer, such as Pandoc, Git or VS Code, the **Install** button in its details opens a menu: **Install for all users** or **Install for the current user**, for that install only. **Install** itself still follows your setting. The details also say how an installed package is installed.

### Fixed

- **Updates keep a package the way it's installed.** An update could move a package installed for you alone to all users, with a permission prompt, because WinGet misreads how some packages are installed. Updates now keep the scope Windows Installer reports, and say so when that differs from the scope set in Settings.
- **Going back no longer installs a second copy.** Some packages, such as Pandoc, have one installer for the current user and one for all users. Going back to an older version could pick the other one, so both versions ended up installed and the app said it had worked. Going back now uses the installer that matches how the package is installed, as Windows Installer records it. If you chose a scope in Settings and the package is installed the other way, the confirmation says so. If a second copy still appears, the result says that both versions are installed and which one to uninstall.
- **Packages installed for all users update and uninstall again.** Updating or uninstalling an MSI package installed for all users, such as Pandoc or PowerShell, could fail with "The installer failed with error code 1603": WinGet ran Windows Installer silently, and silently it can't ask for permission to remove a program installed for all users. For those packages the app now lets Windows show its permission prompt, with a small progress window, and the Activity card says a prompt may come. When removing the installed version still fails, the result says that, instead of blaming the installer.
- **The details show the right version after an update.** After an update from the Search page, the row and the package's details kept showing the version from before, and going back used that version too. They now show what's installed.

## 1.2.1

### Fixed

- **The version button is where you click.** In **Available versions**, the button to install, update to or go back to a version now shows on the line of the version you select. It used to sit under the whole list, often out of sight.
- **Excluded packages open their details.** On the Updates page, a package in **Excluded** now opens its details when you click it, like the others, so you can choose another version for it. A package lands there after you go back to an older version.

## 1.2.0

### New

- **Release notes are easier to read.** In a package's details, lists show as lists and quotes as quotes, and section titles such as "Bug Fixes" stand out, instead of a block of text full of `-` and `>` marks. Web addresses in the notes are links.
- **Read a release's notes from GitHub without leaving the app.** Many packages only link to their GitHub release page. For those, the details now offer **Show notes from GitHub**, which shows that release's notes with their headings, lists, tables and code. CartonStash only contacts GitHub when you press the button, images in the notes are never loaded, and links open in your browser. If GitHub can't be reached or has had too many requests from your network, the app says so and when to try again.
- **Install the version you choose, or go back after a bad update.** In a package's details, pick a version in **Available versions** to install it, update to it, or go back to it. Going back asks first and, unless you untick the box, leaves the package out of update checks, so **Update all** doesn't bring the newer version back. Many installers won't install over a newer version: when that happens, the result offers **Uninstall, then install**, which asks first, because the program's settings may be lost.

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
