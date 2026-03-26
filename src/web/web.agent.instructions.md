---
applyTo: 'src/web/**/*.{razor,css,js,cs}'
description: 'UI and browser interop guidance for NMAC web layer'
---

# Web Agent Instructions

## UI Behavior

- Keep dashboard interactions responsive under high chat volume.
- Prefer incremental rendering and avoid expensive per-row recomputation during renders.
- Preserve existing UX behavior for acknowledgment states and stream filtering.

## Avatar and Image Loading

- Use deferred/throttled image hydration patterns in `browserTimeInterop.js`.
- Avoid reintroducing eager image loading for large tables.
- Keep `decoding="async"` / low-priority fetch hints for non-critical avatars when applicable.

## JS Interop

- Add JS functions to `src/web/wwwroot/browserTimeInterop.js` and surface them via `BrowserTimeInterop.cs`.
- Ensure interop calls are safe for prerender/non-interactive phases.
- Catch `JSDisconnectedException` where components may outlive browser circuits.

## Styling

- Prefer minimal, local CSS changes that match existing style language.
- Avoid broad global style overrides unless explicitly requested.

## Validation

- After UI/interop edits, run:
  - `dotnet build src/server/NMAC.csproj -v minimal`
