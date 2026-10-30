# 005 – Package details: Requirements

**Status:** Approved (2026-10-29). Amendment R6 (https icons) approved 2026-10-30
**Depends on:** 001, 002, 003

## Problem

A name and ID aren't enough to decide whether to install something. Users want to know who publishes it,
what it does, its license and what changed, without leaving the app.

## User stories

- **US1.** As a user, I want to open a package and read its description, publisher, license and links before installing.
- **US2.** As a user, I want to see the available versions and release notes.
- **US3.** As a user, I want to install, update or uninstall from the details view.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | When the user selects a row on the Search, Installed or Updates page (click or Enter), the app shall open a details pane for that package. |
| R1.2 | US1   | The pane shall show, when WinGet provides them: name, publisher, short description, description, license (with link), tags, and links to homepage, publisher site, support, privacy policy. |
| R1.3 | US1   | Links shall open in the user's default browser. The app shall not embed web content. |
| R1.4 | US1   | While metadata is loading, the pane shall show placeholders, and the rest of the app shall stay usable. |
| R1.5 | US1   | If metadata isn't available (unmanaged package, offline), then the pane shall show what is known (name, ID, installed version) and a short note. |
| R2.1 | US2   | The pane shall list available versions (newest first), marking the installed one. |
| R2.2 | US2   | When release notes text or a release-notes URL exists, the pane shall show it (text collapsed after ~10 lines, with "Show more"). |
| R3.1 | US3   | The pane shall offer the same Install / Update / Uninstall actions and progress as the row (spec 003 / 004 queue). |
| R4.1 | –     | The pane shall close with Esc or a Close button, and focus shall return to the row that opened it. |
| R5.1 | US1   | When WinGet provides an icon for a package, its row (Search, Installed, Updates) and the details pane shall show it; otherwise a generic package glyph. *(Added 2026-10-29 at the user's request.)* |
| R5.2 | –     | Icons shall load after the list is shown, only for rows on screen, and never delay or block the list. Only http(s) icon URLs are used. |
| R6.1 | –     | Icons shall load only over https. A package whose only icon uses another scheme shall show the generic glyph. This replaces the last sentence of R5.2. *(Security review, 2026-10-30: icons load with no user action, so plain http exposes the request and lets a network attacker supply the image.)* |
| R6.2 | –     | The README shall say that icons load from the addresses in package manifests, so those hosts can see the user's IP address. |

## Out of scope

- Installing a specific (older) version. The version list is information only in v1.
- Screenshots, ratings, reviews: not in WinGet data.

## Open questions

- Pane or page? **Decided: right-side pane on wide windows, full page below 900 px.**
