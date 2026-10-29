# NNN – Feature name: Design

**Status:** Draft
**Requirements:** [requirements.md](requirements.md)

## Overview

One paragraph: the approach and why.

## Components

| Layer | Type | Responsibility | New / changed |
|-------|------|----------------|---------------|
| Core/Services | | | |
| Core/ViewModels | | | |
| App/Views | | | |

## WinGet API usage

Which `Microsoft.Management.Deployment` calls, options and result statuses; how failures map to `WinGetException`.

## Data flow

User action → view model command → service → WinGet → model → UI state.

## UI

Layout sketch, states (empty / loading / results / error), keyboard behaviour, accessible names.

## Error handling and edge cases

## Testing strategy

Which criteria are covered by unit tests, integration tests, or manual checks.

## Risks and alternatives considered
