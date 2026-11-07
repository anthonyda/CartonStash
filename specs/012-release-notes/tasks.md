# 012 – Readable release notes: Tasks

**Status:** In progress (started 2026-11-07)
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
- [ ] **T10.** Set the requirements, design and tasks to `Done`, and update the roadmap.

## Manual verification log

| Criterion | Date | Result | Notes |
|-----------|------|--------|-------|
