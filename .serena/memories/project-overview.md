# Never Miss a Chat (NMAC) — Project Overview

## Purpose
A tool for YouTube livestream creators to track and triage superchats (donations) without getting overloaded near stream end. Provides a spreadsheet-like acknowledgement workflow with real-time updates.

## Tech Stack
- **.NET 10 / C# latest** — server and UI
- **ASP.NET Core** — HTTP endpoints + Blazor Interactive Server
- **Blazor** — interactive server-rendered UI components (Razor Class Library pattern)
- **Entity Framework Core 10** + **Npgsql** — PostgreSQL persistence
- **EFCore.NamingConventions** — snake_case column/table names
- **Wolverine** — event-driven messaging / command bus
- **Refit** — typed YouTube REST API client
- **protobuf-net.Grpc** — gRPC streaming for live chat messages
- **Aspire** — local orchestration (AppHost), service defaults (OTel, health checks)
- **PostgreSQL** — primary database (run via Docker/Aspire container)

## Projects
| Project | Purpose |
|---|---|
| `src/server/NMAC.csproj` | ASP.NET Core web host; domain logic, persistence, background workers |
| `src/web/NMAC.Ui.csproj` | Razor Class Library; Blazor components, UI contracts, JS interop |
| `src/ServiceDefaults/ServiceDefaults.csproj` | Shared Aspire service defaults (telemetry, health, resilience) |
| `src/apphost.cs` | Aspire AppHost — orchestrates DB container + API |
| `src/tester/` | Manual console test app for WebSub notifications |

## Domain Concepts
- **Subscription** — WebSub subscription to a YouTube channel's Atom feed
- **YTVideo** — YouTube video metadata snapshot
- **LiveChatCaptureSession** — background job tracking for streaming live chat
- **LiveSuperChat** — persisted superchat donation (from gRPC stream)
- **LiveFundingDonation** — persisted membership/fan-funding donation
- **ChannelLivePollTarget** — channel handles to probe for live status

## Codebase Structure
```
src/
  apphost.cs                  # Aspire AppHost entry point
  server/
    Program.cs                # App entry + Wolverine setup + 15 seed channels
    ServerCompositionExtensions.cs  # Composition root (all DI + middleware)
    Core/                     # IEndpoint, IUseCase, DbContext, auth handler
    Events/                   # Wolverine event records
    Subscriptions/            # WebSub protocol lifecycle
    Videos/                   # Video ingestion + YouTube REST client
    LiveStreams/               # Live chat capture workers, gRPC streaming, query service
    Migrations/               # EF Core migrations
    Components/               # App.razor (HTML root), _Imports.razor
    youtube/api/v3/           # gRPC proto + generated interface
  web/
    Components/
      Pages/                  # StreamsPage.razor, StreamPage.razor, StreamPageBase.razor
      Layout/                 # MainLayout.razor, ReconnectModal.razor
      _Imports.razor, Routes.razor
    LiveStreams/               # UI contracts (interfaces, models, enums, SuperChatAckBrowserStore)
    wwwroot/                  # nmacStorage.js (localStorage wrapper)
  ServiceDefaults/
    Extensions.cs             # OTel, health checks, resilience
```
