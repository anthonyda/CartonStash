# 012 – Readable release notes: Tasks

**Status:** In progress: code done, VM checks recorded below; Narrator and Tab check pending (T9)
**Design:** [design.md](design.md)

Tick a task only when its verification passes. Keep tasks small enough for one commit.
Manual checks run in the CartonStash-Test VM (or Windows Sandbox), never on the host.

## Core

- [x] **T1.** `NotesDocument`, `NotesBlock` and `NotesInline` records, and `NotesDocument.PlainText`. Then `WinGetNotesParser`: line kinds, nesting, continuation lines, quotes, section titles, and the inline scan. Save the samples from requirements as test fixtures (Git, ripgrep, bat, Firefox, GitHub.cli, Obsidian). *(R1.1 to R1.5, R1.8, R1.9)*
  - Verify: `WinGetNotesParserTests`: one test per fixture, plus nesting then un-nesting, a blank line between items, a lone `*` and backtick, a `#` line (GitHub.cli) staying plain, `[x](file:///c:/)` staying as written, a bare URL ending in `).`, a URL holding `(`, a 41-character line above a list (not bold), a line ending in `.` above a list (not bold), and a parser exception giving `PlainText`.
- [x] **T2.** `GitHubRelease.TryParse`. *(R2.1)*
  - Verify: `GitHubReleaseTests`: the 8 GitHub URLs from the requirements sample are accepted. Refused: `http:`, `www.github.com`, a port, user info, `/releases`, `/releases/latest`, `/releases/tag/` (empty tag), an owner or repo with bad characters, a non-GitHub host. A tag holding `/` is read whole; query and fragment are ignored.
- [x] **T3.** Add Markdig 1.4.2 to `Directory.Packages.props` and reference it from Core. Run `dotnet restore --force-evaluate` and commit the `packages.lock.json` files. Then write `GitHubMarkdown.Parse`: blocks, inlines, images as links, the HTML scanner, link resolution, and dropping a first heading that repeats the title. Save the PowerToys `v0.102.3101` and ripgrep `15.2.0` release bodies as fixtures. *(R2.4 to R2.7)*
  - Verify: `GitHubMarkdownTests`: each element in R2.4 (heading levels, emphasis, strikethrough, inline code, fenced code keeping its lines, nested, numbered and task lists, quote, pipe table, reference link, rule). `![alt](x.png)` gives "Image: alt" with no other link; `![](x.png)` gives "Image". HTML: `<details><summary>`, `<img alt src>`, `<br>`, `<script>` (content gone), and an entity. A relative link resolves against the page, and `javascript:` stays as text. Both fixtures convert without throwing and keep their tables.
- [x] **T4.** `IReleaseNotesService` and `GitHubReleaseNotesService`: the request and its headers, the source-generated JSON context, manual redirects, the 15 s timeout, the 1 MB cap, the status mapping, and the session cache with `TryGetCached`. *(R2.3, R2.9, R2.11, R2.12)*
  - Verify: `GitHubReleaseNotesServiceTests` with a fake `HttpMessageHandler`: the URL (a tag with `/` escaped to `%2F`) and headers, and no `Authorization` or `Cookie` header. 200 gives Loaded; an empty body gives Loaded with no notes. 404 gives NotFound. 403 with `x-ratelimit-remaining: 0` and 429 with `retry-after` give RateLimited with the right time. A thrown `HttpRequestException` and a timeout give Unreachable; a 500 and bad JSON give Failed. A 301 to `api.github.com` is followed; a 301 to another host and a third redirect give Failed. An oversized `Content-Length` and an oversized body without one give Failed. A second call is served from the cache and makes no request; a failure isn't cached. A caller's cancellation throws `OperationCanceledException`, not Unreachable.
  - Verify (live): `GitHubReleaseNotesServiceIntegrationTests.Fetches_an_old_bat_release` (`Category=Integration`): `sharkdp/bat` `v0.26.1` loads with its title.
- [x] **T5.** `PackageDetailsViewModel`: `ReleaseNotes`, `GitHubRelease`, `CanShowGitHubNotes`, `ShowReleaseNotesSection`, the loading, message and retry flags, `GitHubNotesHeader`, and `LoadGitHubNotesCommand` with its own cancellation. Pass `IReleaseNotesService` through the Search, Installed and Updates view models, and add a fake to `Fakes.cs`. *(R2.2, R2.9, R2.10, R2.11)* *(Amended 2026-11-07: the old "Show more" members are removed in T7, together with the XAML that uses them, so every commit still builds (Q4).)*
  - Verify: `PackageDetailsViewModelTests`: opening a package makes no service call. The button makes one call and shows GitHub's notes and header. Reopening a package shows cached notes with no call. Switching package or closing the pane during a call cancels it, and a late result is dropped. Each failure kind sets its message, with Retry only for Unreachable and Failed, and keeps WinGet's notes. A package with only a GitHub link shows the section. `MessageFlagTests` is extended with the new flags.

## App

- [x] **T6.** `NotesView` control: builds its elements from the document (paragraphs, headings with `HeadingLevel`, lists, quotes, code, tables, rules, links through `Launcher`), uses only theme brushes, and handles collapsing with "Show more" / "Show less". *(R1.6, R1.7, R2.4, R2.8)*
  - Verify: `dotnet build`. The on-screen checks are in T9.
- [x] **T7.** `PackageDetailsPane`: put `NotesView` in place of the `TextBlock`, and add the button, header, loading line and message line with Retry, each with an accessible name and a live region where the design says. (`App.xaml.cs` registers `IReleaseNotesService`, with its own `SocketsHttpHandler` and the `CartonStash/{version}` user agent, since T5: the list view models need it.) Remove `ShowAllReleaseNotes`, `ReleaseNotesIsLong`, `ReleaseNotesMaxLines` and `ToggleReleaseNotesCommand` from the view model, along with their test. *(R2.1, R2.3, R2.11)*
  - Verify: `dotnet build`, then `dotnet test`. The on-screen checks are in T9.

## Docs and packaging

- [x] **T8.** PRIVACY.md: add a "Release notes from GitHub" bullet under Internet connections and update the date. README: extend the network sentence. Add `THIRD-PARTY-NOTICES.md` (Markdig's BSD-2 notice and the existing MIT dependencies), included in the app package as content. Add a "New" entry to the next version's CHANGELOG section. *(R3.1)*
  - Verify: review in the PR. `.\tools\Publish-Release.ps1` output holds `THIRD-PARTY-NOTICES.md` in both the zip and the MSIX.

## Manual checks

- [ ] **T9.** In the VM, on a build from this branch, record the results in the log below. *(R1.1 to R1.9, R2.1 to R2.12)*
  - WinGet's text: Git (section titles in bold, bullets), ripgrep (wrapped items, quote), GitHub.cli (the `#` lines stay plain), Firefox (blank lines between items), a long one collapsed with "Show more" only when it's taller than about 10 lines.
  - From GitHub: PowerToys (button, header, the repeated title heading dropped, the table scrolls sideways, links open the browser), Notepad++, Windows Terminal. The line breaks match the release page on github.com.
  - A package whose link isn't GitHub (VS Code): link only, no button.
  - Offline (VM network disconnected): "Couldn't reach GitHub." and Retry works once the network is back.
  - Rate limit: use up the 60 requests from the VM with a script, then press the button and check the time in the message.
  - Reopen a package whose notes were loaded: they show at once (check with the VM offline).
  - Keyboard: Tab reaches the button, the links in the notes and "Show more". Narrator reads headings, list items and the loading and message lines.
  - Light, dark and high-contrast themes.
  - Time from the button to drawn notes on a large release (PowerToys `v0.102.2933.0`). If drawing takes over 200 ms, add batching to `NotesView` (design, Error handling).
  - *2026-11-07: everything above passed in the VM except a hand-run Narrator and Tab pass, which a script can't do; see the log.*
- [ ] **T10.** Set the requirements, design and tasks to `Done`, and update the roadmap.

## Manual verification log

All runs 2026-11-07 in the CartonStash-Test VM (restored from `clean`), WinGet 1.29, Store MSIX 1.1.99 built from this branch at
`23eb0bd`, driven by `guest\n012a.ps1` to `n012e.ps1` through UI Automation, with screenshots checked. An earlier run on `7cc1ee6`+T7/T8
found three problems, fixed in `23eb0bd` and passing below: GitHub alerts showed `[!NOTE]` as text, table and code borders vanished
in dark, and the button stayed next to Retry. No crashes in any phase.

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
| R1.1, R1.5, R1.9 | 2026-11-07 | Pass | Git: "Bug Fixes" in bold, two bullets with hanging indents, the paragraph above kept with its line break. Firefox: "New" and "Fixed" bold, items separated by blank lines stay one list |
| R1.1, R1.2 | 2026-11-07 | Pass | ripgrep: the quote has its bar and no `>` marks, wrapped items joined ("…binaries for / ripgrep."), the colon titles in bold |
| R1.3 | | Unit test only | GitHub.cli's current notes have no `#` line (a newer release than the sample). `WinGetNotesParserTests.GitHub_cli_hash_lines_are_not_headings` |
| R1.6, R2.8 | 2026-11-07 | Pass | "Show more" only on ripgrep, Firefox, PowerToys, Notepad++ and Terminal, none on Git; Show more / Show less toggle and keep the link in place |
| R1.7 | 2026-11-07 | Partial | UI Automation reads the paragraphs and list items in order (TextPattern dump in the logs). Narrator itself not run (T9 stays open) |
| R2.1 | 2026-11-07 | Pass | Button on the GitHub links (Git, ripgrep, GitHub CLI, PowerToys, Notepad++, Terminal, Oh My Posh); VS Code (`code.visualstudio.com`): link only |
| R2.2 | 2026-11-07 | Pass | The section shows the button and no GitHub text until it is pressed. No request was observed before a press: the button-only state survives until then, and the rate-limit run (R2.11) counts down only with presses |
| R2.3, R2.4 | 2026-11-07 | Pass | PowerToys: header "Release v0.102.3101.0 · 6 November 2026 · from GitHub", the repeated title heading left out, link to v0.102.2933.0, "Installer Hashes" and "Highlights" headings, the 3-column table. Notepad++, Terminal loaded too |
| R2.4 (alerts) | 2026-11-07 | Pass | Terminal: `> [!NOTE]` shows as a quote led by "Note" in bold |
| R2.4 (line breaks) | 2026-11-07 | Pass | ripgrep's GitHub page has `<br>` where its notes break a line (checked against github.com during T3); the app keeps the same breaks |
| R2.7 | | Not run | Links weren't clicked (it would open Edge in the VM). Every link is a `Hyperlink` whose Click calls `Launcher.LaunchUriAsync`, like the pane's other links (005 R1.3) |
| R2.9 | 2026-11-07 | Pass | PowerToys reopened shows GitHub's notes at once without pressing, online and with the VM's network disconnected |
| R2.10 | | Unit test only | `PackageDetailsReleaseNotesTests`: switching package and closing the pane cancel the request |
| R2.11 (offline) | 2026-11-07 | Pass | Network disconnected: "Couldn't reach GitHub." after 0.24 s, with Retry in the button's place; Retry offline gives the same line. Network back: Retry loads Terminal's notes in 0.55 s |
| R2.11 (limit) | 2026-11-07 | Pass | 48 anonymous requests from the VM used up the limit (12 already used that hour); Oh My Posh then says "GitHub limits requests without sign-in. Try again after 7:23 PM.", which is GitHub's `x-ratelimit-reset` in local time. No Retry, button kept |
| R2.11 (not found) | | Unit test only | No live package with a missing release tag was found |
| R2.12 | | Unit test only | `GitHubReleaseNotesServiceTests`: redirects, size cap, timeout |
| Q5 keyboard | 2026-11-07 | Partial | UI Automation: every link in the notes is keyboard focusable (16 on PowerToys). Tab order not walked by hand |
| Q5 themes | 2026-11-07 | Pass | Light, dark (app theme Dark) and high contrast (system, on then off): text, quote bar, table lines, links and header readable in all three |
| Load time | 2026-11-07 | Pass | Button to notes shown, network included: PowerToys 0.5 to 2.3 s, Notepad++ 0.5 s, Terminal (long notes) 0.55 s. Drawing wasn't timed on its own; it is part of those times, so no batching added. Revisit if a much longer release feels slow |
