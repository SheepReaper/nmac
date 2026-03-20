# Code Style & Conventions

## C# Style
- **File-scoped namespaces** (`namespace NMAC.LiveStreams;` not `namespace NMAC.LiveStreams { }`)
- **Primary constructors** preferred for constructor injection
- **Collection initialization syntax** (`[.. collection]`, `[]` for empty)
- **Record types** for DTOs and events (immutable, value semantics)
- **Latest C# features** — pattern matching, switch expressions, target-typed new, etc.
- **2 spaces** indentation (general.instructions.md)
- No explicit `this.` unless disambiguation is needed

## Database / EF Core
- **snake_case** for all DB identifiers (via EFCore.NamingConventions)
- **Atomic upserts** via raw SQL `INSERT ... ON CONFLICT`; avoid read-then-write races
- **Npgsql UNNEST()** for efficient batch inserts
- Create **new DbContext scopes** per batch inside long-running background workers

## Naming
- Public types: `PascalCase`
- Private fields: `_camelCase` (with underscore prefix)
- Interfaces: `IPascalCase`
- Constants: `PascalCase`
- Async methods: `PascalCaseAsync()`

## File Organization
- One type per file, file named after the type
- Files grouped by domain feature folder (`Subscriptions/`, `Videos/`, `LiveStreams/`)
- No helper/utility catch-all folders; if a helper is needed, it lives alongside the domain code that uses it

## Blazor Patterns
- Components use `@code { }` blocks (not code-behind `.razor.cs` files)
- **CSS isolation** via `ComponentName.razor.css` files
- **InteractiveServer** render mode applied per-page with `@rendermode InteractiveServer`
- JS interop is **always deferred** to `OnAfterRenderAsync` gated by `RendererInfo.IsInteractive`
- `JSDisconnectedException` must be swallowed in `DisposeAsync`
- UI contract interfaces live in the web project (`NMAC.Ui.LiveStreams`), implementations in server project

## Security
- Protected endpoints use `DeveloperBasicAuth` policy
- HMAC-SHA signature validation on all WebSub content deliveries
- API keys injected at runtime via Aspire configuration (not in source)
- Anti-forgery middleware present (`app.UseAntiforgery()`)

## Event / Messaging
- **Wolverine** for all in-process or durable messaging
- Events are records in `Events/` folder and handled by co-located `*Handler` classes
- `AlwaysUseServiceLocationFor<AppDbContext>()` ensures scoped DB access in Wolverine
