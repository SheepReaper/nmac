# Never Miss A Chat (NMAC)

A YouTube live stream monitoring tool that captures SuperChat and channel membership donations in real time, so you never miss a viewer's support.

**Live demo:** [nevermac.com](https://nevermac.com) — configured for a single channel that streams ~4 hours on a nightly basis.

## Features

- **Stream Catalog** — live dashboard listing all monitored streams with status, SuperChat count, total USD raised, and last activity
- **Donation Dashboard** — per-stream view with real-time SuperChat list (multi-currency with USD conversion), top donators, and acknowledgment tracking
- **Acknowledgment Workflow** — mark donations as Thanked, Glossed Over, or Unthanked; calculates the required ack rate to finish before a target stream end time
- **CSV Export** — export all donations for a stream
- **Auto-Detection** — polls YouTube channel pages to detect when a stream goes live and starts capturing automatically
- **WebSub Feed Ingestion** — subscribes to YouTube Atom feeds via PubSubHubbub to track new and updated videos

## Architecture

```
src/
  apphost.cs              # .NET Aspire orchestration
  server/NMAC.csproj      # Backend: API, workers, database, Blazor host
  web/NMAC.Ui.csproj      # Razor UI component library
  ServiceDefaults/        # Shared Aspire service configuration
```

**Tech stack**

| Concern | Technology |
|---|---|
| Runtime | .NET 10 / ASP.NET Core |
| UI | Blazor Server + Bulma CSS |
| Database | PostgreSQL via EF Core (Npgsql) |
| Orchestration | .NET Aspire |
| Message bus | Wolverine |
| YouTube REST API | Refit |
| YouTube Live Chat | protobuf-net gRPC |
| Currency conversion | Frankfurter API |
| Observability | OpenTelemetry (metrics + tracing) |

## How It Works

### Live Stream Detection

`ChannelLiveDetectionWorker` polls configured YouTube channel handles (e.g. `@EnforcerOfficial`) at a configurable interval. When a live stream is detected, a PostgreSQL advisory lock prevents duplicate events in multi-instance deployments. A `VideoAdded` event triggers the capture pipeline.

### Chat Capture Pipeline

1. `LiveStreamFoundHandler` fetches video metadata and `liveChatId` from the YouTube Data API v3
2. `LiveChatCaptureWorker` claims up to 5 concurrent sessions; recovers stale sessions automatically
3. `LiveChatStreamProcessor` opens a gRPC stream to the YouTube Live Chat API, upserts `LiveSuperChat` and `LiveFundingDonation` records, and converts amounts to USD via Frankfurter

### WebSub Feed Ingestion

The server registers a WebSub subscription with YouTube for each channel. YouTube pushes Atom feed entries to `/webhooks/youtube/videos/{slug}` on each video publish or update. `SubscriptionRefreshWorker` renews subscriptions before they expire.

### Real-Time UI Updates

`LiveStreamUpdateNotifier` (singleton) routes updates to subscribed Blazor components via `InvokeAsync` + `StateHasChanged`, with no SignalR or WebSocket configuration required beyond Blazor Server's built-in circuit.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (for PostgreSQL via Aspire, or bring your own)
- YouTube Data API v3 key with the **YouTube Data API v3** service enabled

### Obtaining a YouTube Data API v3 key

1. Go to the [Google Cloud Console](https://console.cloud.google.com/) and create or select a project.
2. Navigate to **APIs & Services → Library**, search for **YouTube Data API v3**, and click **Enable**.
3. Navigate to **APIs & Services → Credentials**, click **Create Credentials → API key**.
4. Optional but recommended: restrict the key to **YouTube Data API v3** under *API restrictions*, and restrict it to your server's IP under *Application restrictions*.
5. Copy the key — this is your `YTClient:ApiKey` value.

For additional detail see [Google's official guide](https://developers.google.com/youtube/registering_an_application).

## Getting Started

**1. Configure secrets**

```bash
cd src/server
dotnet user-secrets set "YTClient:ApiKey" "<your-youtube-api-key>"
dotnet user-secrets set "DeveloperBasicAuth:Username" "<admin-username>"
dotnet user-secrets set "DeveloperBasicAuth:Password" "<admin-password>"
```

Or set the Aspire parameters `yt-api-key`, `dev-access-username`, and `dev-access-password` in your environment / secrets store.

**2. Run via Aspire (recommended)**

```bash
cd src
dotnet run --project apphost.cs
```

Aspire will start PostgreSQL in a container, apply migrations, and seed initial subscriptions and channel handles defined in `Program.cs`. The Aspire dashboard will show all resource URLs.

**3. Run server only** (requires an external PostgreSQL instance)

```bash
cd src/server
dotnet run
```

Set `ConnectionStrings__nmac` to your PostgreSQL connection string.

## Admin API

All admin endpoints require HTTP Basic Auth (`dev-access-username` / `dev-access-password`).

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/add-video/{videoId}` | Manually queue a video for capture |
| `POST` | `/add-channel/{channelId}` | Subscribe to a channel's YouTube feed |
| `POST` | `/remove-channel/{channelId}` | Unsubscribe from a channel feed |
| `POST` | `/add-channel-handle/{handle}` | Enable live-stream polling for a channel handle |
| `POST` | `/remove-channel-handle/{handle}` | Disable live-stream polling |
| `GET` | `/webhooks/youtube/videos/{slug}` | WebSub challenge verification |
| `POST` | `/webhooks/youtube/videos/{slug}` | WebSub content distribution callback |

See [tests.http](tests.http) for ready-to-run examples.

## Database

Migrations run automatically on startup. To run them manually:

```bash
cd src/server
dotnet ef database update
```

## Configuration Reference

| Key | Description |
|---|---|
| `YTClient:ApiKey` | YouTube Data API v3 key |
| `DeveloperBasicAuth:Username` | Basic auth username for admin endpoints |
| `DeveloperBasicAuth:Password` | Basic auth password for admin endpoints |
| `ConnectionStrings__nmac` | PostgreSQL connection string |
| `ChannelLivePolling:IntervalSeconds` | Polling interval for live detection (default: 60) |
| `ChannelLivePolling:RecheckIntervalSeconds` | Minimum re-publish throttle per video (default: 30) |

## Known Limitations

### WebSub feed subscriptions do not detect livestreams

YouTube only pushes PubSubHubbub (WebSub) notifications when a channel publishes a regular (non-broadcast) video. Livestream start/end events are **not** delivered via the feed. As a result, `SubscriptionRefreshWorker` and the WebSub ingestion path cannot trigger capture for a live stream — the polling-based `ChannelLiveDetectionWorker` is the only mechanism that actually works.

The WebSub implementation is kept in the codebase as a fully working example of a WebSub/PubSubHubbub subscriber (challenge verification, HMAC-signed payload validation, subscription renewal). If YouTube ever extends feed notifications to live events, the plumbing is already there.

### Charity and fundraiser donation events are not captured

When a livestream contains a YouTube Charity or Creator Fundraiser event, the associated donation messages behave similarly to SuperChats but do **not** appear in the gRPC live chat stream — at least none were observed during testing. The relevant protobuf field index may simply be undocumented. `LiveFundingDonation` records are written for YouTube-native fan funding events, but charity fundraiser amounts are silently absent.

### gRPC stream connection lifetime is capped at ~10 seconds

Despite YouTube's gRPC live chat API advertising a streaming interface, in practice each server-sent stream closes after roughly 10 seconds regardless of activity. The app works around this by reconnecting immediately (see `LiveChatStreamProcessor`) and by tuning polling intervals to make each short-lived connection as productive as possible. This still burns API quota faster than a conventional polling approach would — factor that in when sizing your quota allocation.

## Security Notes

- `.env` files and the `infra/` directory are excluded from source control — never commit credentials
- Donation acknowledgment state is stored in browser `ProtectedLocalStorage` (encrypted, per-user, per-stream)
- Admin endpoints are protected by HTTP Basic Auth; use HTTPS in all non-local environments

## License

[MIT](LICENSE)
