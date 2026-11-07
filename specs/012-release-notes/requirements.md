# 012 – Readable release notes: Requirements

**Status:** Done (2026-11-07). Approved 2026-11-06, with the proposed answers to Q1 and Q2
**Depends on:** 005

## Problem

The details pane shows WinGet's release notes as one plain block of text, so lists and quotes show their `-` and `>`
markers. Many packages have no notes text at all, only a link: in a sample of 12 popular packages, 4 (PowerToys,
Notepad++, Oh My Posh, Windows Terminal) had only a link, and every one of those links was a GitHub release page.
Users have to leave the app to find out what changed.

## What the data looks like (measured 2026-11-06)

- **WinGet's text** (`CatalogPackageMetadata.ReleaseNotes`) is not really Markdown. The tools that publish to
  winget-pkgs remove most of it before it reaches the manifest: headings become plain lines ("Bug Fixes"), code
  blocks are flattened, and links keep only their text. What is left is `- ` list items (sometimes with indented
  continuation lines) and `> ` quotes. A leading `#` is not a heading: in `GitHub.cli` 2.99.0 it is a shell comment
  from a flattened code block (`# Attach files when creating or editing an issue`).
- **GitHub release notes** are real GitHub Markdown, with headings, tables, code, reference-style links and
  sometimes HTML. GitHub's public API returns them without sign-in, limited to 60 requests an hour per IP address.

## User stories

- **US1.** As a user, I want WinGet's release notes laid out as lists and quotes, not as raw markers, so they are quick to scan.
- **US2.** As a user, when a package's release notes are a GitHub release page, I want to read them in the app.

## Acceptance criteria (EARS)

### R1. Formatting WinGet's text

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | A line that starts with `- `, `* ` or `• ` (after any indent) shall show as a bulleted list item with a hanging indent. A following line that is indented and has no marker shall join that item. A marker indented deeper than the item above shall start a nested level. |
| R1.2 | US1   | A line that starts with `> ` shall show as a quote: indented, with the marker removed. Consecutive quote lines shall form one quote. |
| R1.3 | US1   | `**bold**`, `` `code` `` and `[text](https://…)` shall be formatted when both delimiters are on the same line. Anything else, including a leading `#`, `_` and an unpaired `*` or backtick, shall show exactly as written. |
| R1.4 | US1   | A plain `http` or `https` address in the text shall be a link that opens in the default browser (005 R1.3). Addresses with any other scheme shall stay plain text. |
| R1.5 | US1   | Blank lines shall separate paragraphs. All other line breaks shall be kept as written. |
| R1.6 | US1   | Long notes shall stay collapsed with "Show more" as in 005 R2.2, counting the lines as displayed. |
| R1.7 | US1   | The text shall stay selectable, and a screen reader shall read it in order, list items included. |
| R1.8 | US1   | If the text can't be formatted for any reason, then the pane shall show it as plain text, as it does today. |
| R1.9 | US1   | A line of 40 characters or fewer that doesn't end in `.`, `!` or `?` and sits directly above a list item shall show in bold, as a section title. *(Q1, decided 2026-11-06.)* |

### R2. Release notes from GitHub

| ID    | Story | Criterion |
|-------|-------|-----------|
| R2.1  | US2   | When a package's release-notes URL is a GitHub release page (exactly `https://github.com/{owner}/{repo}/releases/tag/{tag}`), the pane shall offer a "Show notes from GitHub" button. For any other URL the pane shall keep only the "Release notes online" link. |
| R2.2  | US2   | The app shall contact GitHub for release notes only when the user presses that button. Opening the details pane shall not. |
| R2.3  | US2   | When the user presses the button, the app shall request that one release from GitHub's public API (`https://api.github.com`), with no sign-in or token, sending only the owner, repository and tag from the URL. The pane shall show that it is loading and the rest of the app shall stay usable. |
| R2.4  | US2   | When the request succeeds, the Release notes section shall show the release's title and publish date and its notes formatted as GitHub Markdown: headings, paragraphs, bold, italic, strikethrough, inline code, code blocks (monospace, line breaks kept), lists (nested, numbered, task lists), quotes, links (reference-style too), tables and horizontal rules. It shall replace WinGet's text in that section and be labelled as coming from GitHub. The "Release notes online" link shall stay. |
| R2.5  | US2   | Images in the notes shall not be loaded. Each one shall show as a link with its alt text, or "Image" when it has none. |
| R2.6  | US2   | HTML in the notes shall not be rendered or run. The app shall show the text inside the tags and drop the tags, except that `<img>` shall show as an image link (R2.5) and `<br>` as a line break. |
| R2.7  | US2   | Links in the notes shall open in the default browser. Relative links shall resolve against the release page. A link that is not `http` or `https` after that shall show as plain text. |
| R2.8  | US2   | Long notes from GitHub shall be collapsed with "Show more" as in 005 R2.2. |
| R2.9  | US2   | Notes the app has loaded shall be kept for the rest of the session, so opening the same package again shows them without another request. |
| R2.10 | US2   | If the user switches package or closes the pane while a request is running, then the app shall cancel it. |
| R2.11 | US2   | If the request fails, then the pane shall keep WinGet's text, if any, and say what happened in a short line: no connection or no answer within 15 seconds ("Couldn't reach GitHub", with Retry); GitHub's limit reached ("GitHub limits requests without sign-in. Try again after 14:32.", the time taken from GitHub's answer); release not found ("GitHub has no release with this tag"); anything else ("Couldn't load the notes from GitHub", with Retry). |
| R2.12 | –     | The app shall talk to `api.github.com` over https only, shall not follow a redirect to another host, and shall stop reading an answer larger than 1 MB, treating it as a failure (R2.11). |

### R3. Documentation

| ID   | Story | Criterion |
|------|-------|-----------|
| R3.1 | –     | PRIVACY.md and the README shall say that when the user asks for release notes from GitHub, the app contacts `api.github.com`, which can see the user's IP address. |

## Out of scope

- Release notes from other sites (for example `code.visualstudio.com`, `obsidian.md`). Those pages are HTML of every
  shape, and reading them would be scraping. They keep the "Release notes online" link.
- Notes for versions other than WinGet's default install version, and "what changed since the version you have".
- Signing in to GitHub to raise the request limit.
- Linking GitHub's `@user` and `#123` shorthand. They show as plain text.
- Images, embedded video and anything else that would load content from other hosts.

## Open questions

- **Q1. Section titles in WinGet's text.** Lines like "Bug Fixes" or "Platform support:" were headings before the
  publishing tools stripped them. Should a short line (40 characters or fewer, no final `.`, `!` or `?`) that sits
  directly above a list item be shown in bold? *Proposed: yes.* It only changes the weight of a line that is already
  there, and it fits every sample above. It doesn't fit a line that just happens to be short, but that line still reads fine in bold.
  **Decided 2026-11-06: yes, added as R1.9.**
- **Q2. Button or automatic.** R2.2 asks the user each time. Fetching when the pane opens would be smoother, but it
  would contact GitHub without the user asking and run into the 60-an-hour limit when browsing quickly.
  *Proposed: the button, as written.* A setting for automatic loading can come later if users ask for it.
  **Decided 2026-11-06: the button (R2.2 unchanged).**
