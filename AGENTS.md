# AGENTS.md

This repository supports AI coding agents. Use this document as the default working contract for contributors using Copilot/agents.

## Project Summary

Never Miss A Chat (NMAC) is a .NET 10 solution that monitors YouTube livestream chat events and renders a Blazor dashboard.

- Server: `src/server` (`NMAC.csproj`)
- UI component library + static assets: `src/web` (`NMAC.Ui.csproj`)
- Shared Aspire defaults: `src/ServiceDefaults`
- AppHost orchestration: `src/apphost.cs`

## Build and Run

- Build server (transitively builds web + ServiceDefaults):
  - `dotnet build src/server/NMAC.csproj -v minimal`
- Run via Aspire:
  - `dotnet run --project src/apphost.cs`
- Run server only:
  - `dotnet run --project src/server/NMAC.csproj`

## Coding Conventions

- Prefer latest language features supported by project targets.
- Prefer small, focused edits over broad refactors.
- Do not rename public endpoints or DTO fields unless explicitly requested.
- Keep logging structured and use existing logger message style in workers/services.
- Preserve existing formatting/style per file.

## Data and Persistence Rules

- EF Core model is in `src/server/Core/AppDbContext.cs`.
- Migrations live under `src/server/Migrations`.
- If schema changes are required, add migration and ensure runtime startup behavior remains valid.
- Avoid destructive data operations unless explicitly requested.

## Health Checks and Ops

- Health endpoints: `/health` and `/alive`, mapped via `ServiceDefaults`.
- If changing health-check behavior, keep readiness/liveness semantics intact.
- Background workers should follow existing `PeriodicTimer` + scoped DbContext patterns.

## UI and Interop

- Stream dashboard is primarily in `src/server/Components/Pages/StreamPage.razor`.
- Browser JS interop module is `src/web/wwwroot/browserTimeInterop.js` with C# wrapper in `src/web/LiveStreams/BrowserTimeInterop.cs`.
- For expensive UI updates, prioritize throttling/deferred loading patterns already used in this codebase.

## Validation Checklist For Agent PRs

Before considering work complete:

1. Build succeeds with `dotnet build src/server/NMAC.csproj -v minimal`.
2. No accidental edits to unrelated files.
3. README/config docs updated when behavior changes affect operators.
4. New background services are registered in composition extensions.

## Safety and Scope

- Do not commit secrets or add credentials to source-controlled files.
- Keep changes production-safe by default.
- If unsure about YouTube API behavior, document assumptions in code comments or PR notes.
