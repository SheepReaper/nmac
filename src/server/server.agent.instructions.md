---
applyTo: 'src/server/**/*.cs'
description: 'Server-side implementation guidance for NMAC'
---

# Server Agent Instructions

## Architecture

- Keep composition changes centralized in `ServerCompositionExtensions`.
- Prefer dependency injection and scoped services over static state.
- Background workers should be resilient to cancellation and transient failures.

## Persistence

- Use `AppDbContext` via scoped lifetimes.
- Prefer indexed query paths already established in model configuration.
- For cleanup/maintenance operations, use set-based EF operations when appropriate.

## Endpoints and Auth

- Admin endpoints are protected by basic auth policy.
- Preserve existing endpoint routes unless task explicitly requires route changes.

## Logging

- Prefer `LoggerMessage` partial methods for repetitive logs in hot paths.
- Include identifiers (videoId, liveChatId, sessionId) in operational logs.

## Validation

- After edits, run:
  - `dotnet build src/server/NMAC.csproj -v minimal`
