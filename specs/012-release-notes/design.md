# 012 – Readable release notes: Design

**Status:** Approved (2026-11-06)
**Requirements:** [requirements.md](requirements.md)

## Overview

Both kinds of notes become one plain document model in Core (`NotesDocument`: blocks and inlines, no WinUI), so
one App control draws both. Two parsers fill it:

- **WinGet's text** goes through a small hand-written line parser that knows only the few patterns R1 allows. A
  general Markdown parser would read the leftovers wrongly: a `#` line would become a heading, and `_` in file
  names would turn into italics.
- **GitHub's notes** go through Markdig, which handles real GitHub Markdown (R2.4). A converter walks Markdig's
  syntax tree into `NotesDocument`, and that is where images, HTML and links are made safe (R2.5 to R2.7).

A new service in `Core/Services` fetches one release from GitHub's API when the user asks. The details view
model gets a "Show notes from GitHub" command. The pane's release notes `TextBlock` is replaced by a `NotesView`
control that draws a `NotesDocument` and handles "Show more" itself.

This doesn't conflict with the constitution. P1: the notes are only shown; the app never uses them to decide
anything about a package. A2: WinGet calls stay where they are, and the HTTP code lives in `Core/Services` and returns plain
records. P2: GitHub is contacted only when the user presses the button (R2.2).

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Models | `NotesDocument`, `NotesBlock` (Paragraph, Heading, List, Quote, CodeBlock, Table, Rule), `NotesInline` (Text with Bold / Italic / Strike / Code, Link, LineBreak) | The formatted notes as plain records. A `Link` holds an `Uri?` that is always http(s) or null | new |
| Core/Models | `GitHubRelease` (Owner, Repo, Tag), `GitHubNotes` (Title, PublishedAt, PageUrl, Body), `GitHubNotesResult` (Kind: Loaded / Unreachable / RateLimited / NotFound / Failed, plus Notes or ResetAt) | What the service takes and returns | new |
| Core/Services | `WinGetNotesParser.Parse(string)` | Line parser for R1.1 to R1.5 and R1.9. Falls back to plain paragraphs on any exception (R1.8) | new |
| Core/Services | `GitHubMarkdown.Parse(string body, Uri page)` | Markdig to `NotesDocument` (R2.4 to R2.7) | new |
| Core/Services | `GitHubRelease.TryParse(string? url)` | Matches only `https://github.com/{owner}/{repo}/releases/tag/{tag}` (R2.1) | new |
| Core/Services | `IReleaseNotesService`, `GitHubReleaseNotesService(HttpMessageHandler, string userAgent, TimeSpan timeout)` | Request, limits, status mapping (R2.3, R2.11, R2.12), session cache (R2.9) | new |
| Core/ViewModels | `PackageDetailsViewModel` | Builds `ReleaseNotes` (a `NotesDocument?`) from WinGet's text; `LoadGitHubNotesCommand`; loading, message and retry state; cancels on switch and close (R2.10). The "Show more" members move to the view | changed |
| Core/ViewModels | `SearchViewModel`, `InstalledViewModel`, `UpdatesViewModel` | Pass `IReleaseNotesService` to their `PackageDetailsViewModel` | changed |
| App/Controls | `NotesView` | Draws a `NotesDocument`; collapses to about 10 lines with "Show more" (R1.6, R2.8) | new |
| App/Controls | `PackageDetailsPane` | Uses `NotesView`, adds the GitHub button, header and message line | changed |
| App | `App.xaml.cs` | Registers `IReleaseNotesService` with its own `SocketsHttpHandler` | changed |
| Docs | PRIVACY.md, README, new THIRD-PARTY-NOTICES.md | R3.1; Markdig's BSD-2 notice | changed / new |

## WinGet API usage

None new. `CatalogPackageMetadata.ReleaseNotes` and `ReleaseNotesUrl` are already read into `PackageDetails` (spec 005).

## WinGet's text: parsing rules (R1)

The parser reads lines (CRLF normalised, tabs counted as 4 spaces) and classifies each one:

| Line | Becomes |
|------|---------|
| blank | ends the paragraph or quote. Inside a list, a blank line followed by another item keeps the list going (Firefox puts a blank line between items) |
| `^\s*[-*•]\s+text` | list item. Its indent is the marker's column. A deeper indent than the current item nests under it; a shallower one closes levels back to the matching one (R1.1) |
| indented, no marker, right after a list item | joins that item with a line break (ripgrep's wrapped items, R1.1, R1.5) |
| `^\s*>` | quote line, marker and one space removed. Consecutive lines form one quote (R1.2) |
| ≤ 40 chars, no final `.` `!` `?`, and the next non-blank line is a list item | bold paragraph (R1.9). Not a heading, so screen readers don't announce a structure that isn't there |
| anything else | paragraph text. Lines in a row are joined with line breaks (R1.5) |

Inline scan, left to right on each line (R1.3, R1.4):

- `` `code` ``: up to the next backtick on the same line. No other formatting inside.
- `**bold**`: up to the next `**` on the same line, with text in between.
- `[text](url)`: becomes a link only when `url` passes `SafeUri` (http or https). Otherwise the whole thing stays as written.
- Bare `https?://…` up to whitespace or `<>"'`, with trailing `.,;:!?)` left out of the link (unless the URL itself
  holds a matching `(`).
- Anything unmatched (`#`, `_`, a lone `*` or backtick) is plain text.

Any exception gives `NotesDocument.PlainText(text)`: one paragraph per blank-line block, which looks like today (R1.8).

## GitHub's notes

### The request (R2.3, R2.12)

- `GitHubRelease.TryParse` accepts only: scheme https, host `github.com` (any letter case), no port, no user info,
  path `/{owner}/{repo}/releases/tag/{tag…}`. Owner matches `[A-Za-z0-9-]{1,39}` and repo `[A-Za-z0-9._-]{1,100}`. The tag is
  everything after `tag/`, URL-decoded, and must not be empty (tags may contain `/`). Query and fragment are ignored.
  Anything else returns null, and the pane keeps only the link (R2.1).
- Request: `GET https://api.github.com/repos/{owner}/{repo}/releases/tags/{Uri.EscapeDataString(tag)}`, with
  headers `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28` and
  `User-Agent: CartonStash/{app version}` (GitHub rejects requests without one). No token, no cookies.
- Handler: one `SocketsHttpHandler` for the service, with `AllowAutoRedirect = false`, `UseCookies = false`,
  automatic decompression, and the system proxy. GitHub answers with a 301 for a renamed repository. The service
  follows at most 2 redirects itself, and only when `Location` is https on `api.github.com`. Any other redirect counts as Failed.
- Limits: a 15-second timeout covering the whole request, linked with the caller's token (R2.10). The service reads
  headers first (`ResponseHeadersRead`), rejects a `Content-Length` over 1 MB, and reads the body through a stream that stops at
  1 MB + 1 byte. Too large counts as Failed.
- JSON: `System.Text.Json` with a source-generated context for `name`, `tag_name`, `published_at`, `body` and `html_url`.
  A missing or empty `body` is a Loaded result with an empty document, and the pane says "This GitHub release has no notes."

### Status mapping (R2.11)

| Answer | Result | Pane line |
|--------|--------|-----------|
| 200 | Loaded | (none) |
| 404 | NotFound | "GitHub has no release with this tag." |
| 403 or 429 with `x-ratelimit-remaining: 0` (time from `x-ratelimit-reset`, Unix seconds), or with `retry-after` (seconds from now) | RateLimited | "GitHub limits requests without sign-in. Try again after {time, short local format}." |
| `HttpRequestException`, or the 15 s timeout | Unreachable | "Couldn't reach GitHub." + Retry |
| anything else (5xx, bad JSON, too large, bad redirect) | Failed | "Couldn't load the notes from GitHub." + Retry |

The RateLimited line has no Retry button: retrying before the reset time can't work. Pressing "Show notes from GitHub"
again is still possible.

### Session cache (R2.9)

The service keeps successful results in a `ConcurrentDictionary` keyed by the API URL, for the life of the process.
Failures aren't kept, so Retry really asks again. `TryGetCached(GitHubRelease)` lets the view model show cached notes
straight away when a package is opened again.

### Markdown conversion (R2.4 to R2.7)

- Markdig 1.4.2 (BSD-2-Clause) with `UsePipeTables`, `UseTaskLists`, `UseEmphasisExtras(Strikethrough)` and
  `UseAutoLinks`. Line breaks inside a paragraph are kept as line breaks, as GitHub shows them in release notes. This is checked
  against a live release page during implementation.
- Blocks: `HeadingBlock` → Heading (level 1 to 6). `ParagraphBlock` → Paragraph. `ListBlock` → List (ordered, start number,
  task check state per item). `QuoteBlock` → Quote. `FencedCodeBlock` / `CodeBlock` → CodeBlock (text only; no highlighting).
  `Table` → Table (first row is the header). `ThematicBreakBlock` → Rule. Anything else is converted to its plain text.
- Inlines: emphasis → Bold / Italic / Strike flags. `CodeInline` → Code. `LinkInline` → Link, or, when `IsImage`,
  Link with text "Image: {alt}" or "Image" (R2.5), so no image is ever downloaded. Reference-style links are already
  resolved by Markdig. `AutolinkInline` → Link. `LineBreakInline` → LineBreak.
- Links (R2.7): `new Uri(page, url)` with the release's `html_url` as base, then `SafeUri`. A link that fails stays as its text.
- HTML (R2.6): an `HtmlBlock`'s raw text and each `HtmlInline` are reduced with a small tag scanner. `<br>` becomes
  LineBreak; `<img>` becomes an image link from its `alt` and `src` attributes; every other tag is dropped and the text
  between tags is kept, with entities decoded (`WebUtility.HtmlDecode`). Comments `<!-- … -->` are dropped.
  `<script>` and `<style>` drop their content too.
- If the body starts with a heading whose text equals the release title (PowerToys: `# Release v0.102.3101.0`), that heading
  is left out, because the pane already shows the title.

## View model (`PackageDetailsViewModel`)

New members (plain bools for the view, per the x:Bind gotcha in CLAUDE.md):

- `ReleaseNotes` (`NotesDocument?`): from `WinGetNotesParser` when `Details` is set; replaced by GitHub's document after a
  successful load. `HasReleaseNotes` stays.
- `GitHubRelease` (from `Details.ReleaseNotesUrl`). `CanShowGitHubNotes` is true when there is one, nothing is loading, and GitHub's notes
  aren't shown yet.
- `IsLoadingGitHubNotes`, `GitHubNotesMessage`, `HasGitHubNotesMessage`, `CanRetryGitHubNotes`.
- `NotesFromGitHub` (bool) and `GitHubNotesHeader`: "{title} · {published date, long local format} · from GitHub".
- `ShowReleaseNotesSection` is true when `HasReleaseNotes || CanShowGitHubNotes || HasGitHubNotesMessage`. It lets the section appear for
  packages with only a GitHub link.
- `LoadGitHubNotesCommand` (async). It has its own `CancellationTokenSource`, cancelled by `LoadAsync` (another package) and by `Close` (R2.10).
  A result that arrives after cancellation is dropped.
- `LoadAsync` resets all of this, then checks `TryGetCached` after details arrive (R2.9).

Removed: `ShowAllReleaseNotes`, `ReleaseNotesIsLong`, `ReleaseNotesMaxLines`, `ToggleReleaseNotesCommand`, with their tests in
`PackageDetailsViewModelTests`. Collapsing moves to `NotesView`, because only the view knows how tall wrapped text is.

## UI

```
│ Release notes                                 │
│ Release v0.102.3101.0 · 6 November 2026 · from GitHub │   ← only when NotesFromGitHub
│ This hotfix release improves File Explorer …  │
│ Installer Hashes                              │   ← heading
│ ┌──────────┬───────────┬────────┐             │   ← table, scrolls sideways
│ │ …        │ …         │ …      │             │
│ Show more                                     │
│ [Show notes from GitHub]                      │   ← CanShowGitHubNotes
│ ⓘ Couldn't reach GitHub.  Retry               │   ← HasGitHubNotesMessage
│ ⟳ Loading notes from GitHub…                  │   ← IsLoadingGitHubNotes
│ Release notes online ↗                        │
```

`NotesView` (UserControl, `Document` dependency property) rebuilds its children in code when `Document` changes:

- Paragraph and Heading: one `RichTextBlock` each (`IsTextSelectionEnabled`, wrapping). `Run`s with `Bold`, `Italic`,
  `TextDecorations.Strikethrough`, and Cascadia Mono for code. Headings get `AutomationProperties.HeadingLevel` and a
  larger size (20 / 18 / 16, then 14 semibold for levels 4 to 6). Links are `Hyperlink`s whose Click opens the URI with
  `Launcher.LaunchUriAsync`, like the pane's `Link_Click`. Their tooltip and accessible name end with "(opens browser)".
- List: a two-column `Grid` per item (marker: `•`, `1.`, or ☐ / ☑ for tasks; content blocks), indented 16 px per level.
- Quote: a `Border` with a 3 px left bar (`ControlStrokeColorDefaultBrush`) around its blocks.
- CodeBlock: a `Border` (`CardBackgroundFillColorSecondaryBrush`) holding a horizontal `ScrollViewer` with a monospace,
  non-wrapping `RichTextBlock`.
- Table: a `Grid` inside a horizontal `ScrollViewer`, header row semibold, 1 px cell lines. Cells hold `RichTextBlock`s.
- Rule: a 1 px `Border`.
- Only theme brushes are used, so light, dark and high contrast work (Q5).
- Collapsing (R1.6, R2.8): while collapsed, the content sits in a container with `MaxHeight` = 10 × the body line height
  (200 px), clipped. After layout, the control compares the content's desired height with that limit and shows "Show more"
  only when it is taller. "Show more" / "Show less" is a `HyperlinkButton` below the content; it keeps focus when pressed.
  A new `Document` collapses again.
- The button: `Button` "Show notes from GitHub", accessible name "Show release notes from GitHub". The loading line is a small
  `ProgressRing` and text, announced through a live region (`AutomationProperties.LiveSetting="Polite"`), as is the message line.

## Data flow

Row selected → `LoadAsync` → `Details` → `ReleaseNotes = WinGetNotesParser.Parse(...)`, `GitHubRelease = TryParse(url)`,
cached GitHub notes shown if present.
Button → `LoadGitHubNotesCommand` → `IReleaseNotesService.GetAsync(release, ct)` → result →
Loaded: `ReleaseNotes = GitHubMarkdown.Parse(body, page)`, header set; other kinds: message set, WinGet's notes stay (R2.11).

## Error handling and edge cases

- Very long GitHub notes (bodies can be up to 125,000 characters): parsing runs on a worker thread (`Task.Run`). Drawing builds
  every element even while collapsed. Implementation measures the time on a large release, and if it is over 200 ms, `NotesView` builds blocks in batches.
- A package with GitHub notes and no WinGet text: the section shows only the button until the user presses it.
- The same release reached from two packages, or from Search and Installed: one cache entry, shared.
- Tag with a `/`, `+` or spaces: escaped as one path segment, so `a/b` becomes `a%2Fb`. GitHub accepts this; checked in an integration test.
- A `Hyperlink` inside a `RichTextBlock` is reachable with Tab, so keyboard users can reach every link (Q5).

## Testing strategy

| Criteria | How |
|----------|-----|
| R1.1 to R1.5, R1.8, R1.9 | Unit tests on `WinGetNotesParser`, with the real samples from requirements (Git, ripgrep, bat, Firefox, GitHub.cli, Obsidian) as fixtures, plus edge cases: nesting and un-nesting, a lone `*`, a `#` line, `[x](file:///…)`, a URL ending in `).` |
| R1.6, R1.7, R2.8 | Manual check in the VM: collapsed height, "Show more" only when needed, Narrator reads the lists, Tab reaches the links |
| R2.1 | Unit tests on `GitHubRelease.TryParse`: valid URLs, `http`, `www.github.com`, a port, user info, extra segments, `/releases/latest`, an empty tag, a tag with `/` |
| R2.2, R2.9, R2.10 | View model tests with a fake `IReleaseNotesService`: no call on open; a call on the button; a cached result shown on reopen; a cancelled result dropped |
| R2.3, R2.11, R2.12 | Service tests with a fake `HttpMessageHandler`: request URL and headers, and no `Authorization` header; each status in the mapping table; reset time; a timeout (a short timeout passed to the constructor); a 301 to `api.github.com` followed and one to another host refused; an oversized `Content-Length` and an oversized stream |
| R2.4 to R2.7 | Unit tests on `GitHubMarkdown` with the PowerToys and ripgrep release bodies saved as fixtures, plus small cases for each element, images, HTML (`<details>`, `<img>`, `<br>`, `<script>`) and relative links |
| R2.3 live | One integration test (`Category=Integration`) that fetches a fixed old release (`sharkdp/bat` `v0.26.1`) and checks the title |
| R3.1 | Review of PRIVACY.md and the README in the PR |

## Risks and alternatives considered

- **A full Markdown parser for WinGet's text too.** Rejected: the GitHub.cli sample shows that leftover `#` lines aren't
  headings. A conservative parser can only under-format, and that looks like today.
- **Writing a GitHub Markdown parser ourselves.** Rejected: tables, reference links and nested lists are a lot of edge cases, and
  Markdig is the standard .NET choice. It brings the first notice requirement into the app (BSD-2 asks for the copyright notice
  in binary distributions), so this spec adds THIRD-PARTY-NOTICES.md to the package and lists the existing MIT dependencies there too.
- **Rendering HTML in a WebView2.** Rejected: 005 R1.3 forbids embedded web content, and it would load images and scripts.
- **The 60-an-hour limit** is per IP address, so other apps on the same network count against it. Only the button uses it (R2.2), and
  the cache avoids repeat requests. The message says when to try again.
- **Markdig updates.** A Markdig update could change how some notes look. Its version is pinned in `Directory.Packages.props`, and the fixtures catch changes.
