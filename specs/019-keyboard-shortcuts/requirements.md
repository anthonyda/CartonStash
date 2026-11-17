# 019 – Keyboard shortcuts: Requirements

**Status:** Approved
**Depends on:** 001, 002, 004, 006, 009

## Problem

Every control can be reached with Tab (constitution Q5), but apart from Esc and Enter nothing has a shortcut. A keyboard
user has to tab through the navigation pane to change pages, to reach the search box and to press Refresh. Windows' app
guidelines expect keyboard accelerators for frequent commands, as other Windows apps provide them.

## User stories

- **US1.** As a keyboard user, I want to jump to a page or the Activity pane with one shortcut so that I don't tab through the navigation pane.
- **US2.** As a keyboard user, I want to put the cursor in the page's search or filter box with Ctrl+F so that I can type
  right away.
- **US3.** As a keyboard user, I want to refresh the Installed and Updates lists with F5 so that I don't have to find the
  button.
- **US4.** As any user, I want to see a command's shortcut where I'd look for it so that I can learn it.

## Acceptance criteria (EARS)

| ID   | Story | Criterion |
|------|-------|-----------|
| R1.1 | US1   | When the user presses Ctrl+1, Ctrl+2 or Ctrl+3, the app shall show the Search, Installed or Updates page, as if its navigation item had been clicked. |
| R1.2 | US1   | When the user presses Ctrl+5, the app shall show the Settings page. |
| R1.3 | US1   | When the user presses Ctrl+4, the app shall open the Activity pane, or close it if it is open and not pinned, as the Activity navigation item does. |
| R2.1 | US2   | When the user presses Ctrl+F on the Search page, the app shall focus the search box and select its text. |
| R2.2 | US2   | When the user presses Ctrl+F on the Installed page, the app shall focus its filter box and select its text. On a page without a search or filter box (Updates, Settings), the keys shall do nothing. |
| R2.3 | US2   | When the user presses Ctrl+F while focus is in the Activity pane, the app shall focus the pane's filter box instead of the page's box. |
| R3.1 | US3   | When the user presses F5 or Ctrl+R on the Installed or Updates page, the app shall run the same refresh as the page's Refresh (Check again) button. |
| R3.2 | US3   | While a refresh is already running, F5 and Ctrl+R shall do nothing. (The button restarts a running refresh; a held key must not restart it over and over.) |
| R4.1 | US4   | The tooltip of each button that has a shortcut shall name it (for example "Refresh (F5)"). |
| R4.2 | US4   | The tooltips of the navigation items shall name their shortcuts. |
| R4.3 | US4   | Esc shall keep its current meanings (close the details pane, empty the Activity filter) and show no "Esc" tooltips. |
| R5.1 | all   | While a dialog is open, the shortcuts in R1 to R3 shall do nothing. |
| R5.2 | all   | The shortcuts shall work wherever the focus is in the main window, including in a text box, except where the focused control uses the same keys itself. |
| R5.3 | all   | Narrator shall announce each shortcut with its control (`AutomationProperties.AcceleratorKey`). |

## Out of scope

- Shortcuts for install, update, uninstall and Update all: they change the PC, and Update all asks for no confirmation
  (004 R9). A key pressed by mistake must not start one.
- Access keys (Alt plus a letter, with the overlaid key tips).
- A shortcuts help page or dialog.
- Shortcuts that the user can change.

## Decisions

- Numbers follow the navigation pane's order: Search, Installed, Updates, Activity, Settings (Ctrl+1 to Ctrl+5).
- Only Ctrl+F for the search and filter boxes, not Ctrl+E.
