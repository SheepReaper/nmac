using System.Data;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NMAC.Core;
using NMAC.Events;

using Wolverine;

namespace NMAC.LiveStreams;

public partial class ChannelLiveDetectionWorker(
    IHttpClientFactory httpClientFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<ChannelLivePollingOptions> options,
    TimeProvider tp,
    ILogger<ChannelLiveDetectionWorker> logger
) : BackgroundService
{
    public const string ProbeClientName = "youtube-live-probe";

    [LoggerMessage(EventId = 6101, Level = LogLevel.Information, Message = "Channel live polling worker started. Handles={HandleCount}, pollEvery={PollInterval}s, recheckEvery={RecheckInterval}s.")]
    private partial void LogWorkerStarted(int handleCount, int pollInterval, int recheckInterval);

    [LoggerMessage(EventId = 6102, Level = LogLevel.Information, Message = "Channel live polling disabled by configuration.")]
    private partial void LogWorkerDisabled();

    [LoggerMessage(EventId = 6103, Level = LogLevel.Information, Message = "Channel live polling has no enabled handles in DB.")]
    private partial void LogNoEnabledHandles();

    [LoggerMessage(EventId = 6104, Level = LogLevel.Information, Message = "Live candidate detected from {Handle}: video {VideoId}. Publishing VideoAdded.")]
    private partial void LogVideoDetected(string handle, string videoId);

    [LoggerMessage(EventId = 6105, Level = LogLevel.Information, Message = "Skipping publish for {Handle} -> {VideoId}; next eligible probe at {NextEligibleAt}.")]
    private partial void LogPublishThrottled(string handle, string videoId, string nextEligibleAt);

    [LoggerMessage(EventId = 6106, Level = LogLevel.Information, Message = "Skipping publish for {Handle} -> {VideoId}; active capture session already exists.")]
    private partial void LogActiveSessionSkip(string handle, string videoId);

    [LoggerMessage(EventId = 6107, Level = LogLevel.Warning, Message = "Failed live probe for handle {Handle}. Error: {Error}")]
    private partial void LogProbeFailed(string handle, string error);

    [LoggerMessage(EventId = 6108, Level = LogLevel.Information, Message = "Skipping publish for {Handle} -> {VideoId}; another instance is processing this candidate.")]
    private partial void LogDistributedPublishSkip(string handle, string videoId);

    [LoggerMessage(EventId = 6109, Level = LogLevel.Information, Message = "Channel live polling worker stopped.")]
    private partial void LogWorkerStopped();

    private readonly Dictionary<string, HandlePollState> _pollState = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cfg = options.Value;

        if (!cfg.Enabled)
        {
            LogWorkerDisabled();
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, cfg.PollIntervalSeconds));
        var recheckInterval = TimeSpan.FromSeconds(Math.Max(30, cfg.RecheckIntervalSeconds));

        var http = httpClientFactory.CreateClient(ProbeClientName);
        if (!http.DefaultRequestHeaders.UserAgent.Any())
            http.DefaultRequestHeaders.UserAgent.ParseAdd("subscriber-live-detector/1.0");

        LogWorkerStarted(0, (int)pollInterval.TotalSeconds, (int)recheckInterval.TotalSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var handles = await GetEnabledHandlesAsync(stoppingToken);
                if (handles.Length == 0)
                {
                    LogNoEnabledHandles();
                    await Task.Delay(pollInterval, stoppingToken);
                    continue;
                }

                foreach (var handle in handles)
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    try
                    {
                        await ProbeHandleAsync(http, handle, recheckInterval, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogProbeFailed(handle, ex.Message);
                    }
                }

                await Task.Delay(pollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }

        LogWorkerStopped();
    }

    private async Task ProbeHandleAsync(HttpClient http, string handle, TimeSpan recheckInterval, CancellationToken ct)
    {
        var uri = new Uri($"https://www.youtube.com/{handle}/live");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

        var videoId = await TryResolveCandidateVideoIdAsync(response, ct);
        if (string.IsNullOrWhiteSpace(videoId))
        {
            _pollState.Remove(handle);
            return;
        }

        var now = tp.GetUtcNow();
        if (_pollState.TryGetValue(handle, out var state)
            && string.Equals(state.VideoId, videoId, StringComparison.Ordinal)
            && now < state.NextEligibleAt)
        {
            LogPublishThrottled(handle, videoId, state.NextEligibleAt.ToString("O"));
            return;
        }

        var wasPublished = await TryPublishLiveCandidateAsync(handle, videoId, now, recheckInterval, ct);
        if (!wasPublished)
            _pollState[handle] = new HandlePollState(videoId, now + recheckInterval);
    }

    private async Task<bool> TryPublishLiveCandidateAsync(string handle, string videoId, DateTimeOffset now, TimeSpan recheckInterval, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lockKey = ComputeAdvisoryLockKey(videoId);

        if (!await TryAcquireAdvisoryLockAsync(db, lockKey, ct))
        {
            LogDistributedPublishSkip(handle, videoId);
            return false;
        }

        try
        {
            if (await HasActiveCaptureSessionAsync(db, videoId, ct))
            {
                LogActiveSessionSkip(handle, videoId);
                return false;
            }

            LogVideoDetected(handle, videoId);
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new VideoAdded(videoId));
            _pollState[handle] = new HandlePollState(videoId, now + recheckInterval);
            return true;
        }
        finally
        {
            await ReleaseAdvisoryLockAsync(db, lockKey, ct);
        }
    }

    private async Task<bool> HasActiveCaptureSessionAsync(AppDbContext db, string videoId, CancellationToken ct)
    {
        return await db.LiveChatCaptureSessions.AnyAsync(
            s => s.VideoId == videoId && (s.State == "Requested" || s.State == "Running"),
            ct);
    }

    private async Task<string[]> GetEnabledHandlesAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.ChannelLivePollTargets
            .Where(t => t.Enabled)
            .Select(t => t.Handle)
            .Distinct()
            .ToArrayAsync(ct);
    }

    private static async Task<bool> TryAcquireAdvisoryLockAsync(AppDbContext db, long key, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select pg_try_advisory_lock(@key)";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = key;
        cmd.Parameters.Add(parameter);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is true;
    }

    private static async Task ReleaseAdvisoryLockAsync(AppDbContext db, long key, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            return;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select pg_advisory_unlock(@key)";
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "key";
        parameter.Value = key;
        cmd.Parameters.Add(parameter);

        await cmd.ExecuteScalarAsync(ct);
    }

    private static long ComputeAdvisoryLockKey(string value)
    {
        unchecked
        {
            const ulong offset = 1469598103934665603;
            const ulong prime = 1099511628211;
            ulong hash = offset;

            foreach (var ch in value)
            {
                hash ^= ch;
                hash *= prime;
            }

            return (long)hash;
        }
    }

    private static async Task<string?> TryResolveCandidateVideoIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
            return null;

        var html = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var canonicalUrl = ExtractCanonicalUrl(html);
        if (string.IsNullOrWhiteSpace(canonicalUrl) || !Uri.TryCreate(canonicalUrl, UriKind.Absolute, out var canonicalUri))
            return null;

        var videoId = TryExtractVideoId(canonicalUri);
        if (string.IsNullOrWhiteSpace(videoId))
            return null;

        var playabilityStatus = ExtractMatchGroup(html, "\\\"playabilityStatus\\\"\\s*:\\s*\\{[^}]*\\\"status\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
        var isLiveNow = ExtractMatchGroup(html, "\\\"liveBroadcastDetails\\\"\\s*:\\s*\\{[^}]*\\\"isLiveNow\\\"\\s*:\\s*(true|false)");

        // Scheduled streams often resolve to canonical watch URLs too; reject known offline/scheduled markers.
        if (string.Equals(playabilityStatus, "LIVE_STREAM_OFFLINE", StringComparison.Ordinal)
            || string.Equals(isLiveNow, "false", StringComparison.OrdinalIgnoreCase))
            return null;

        return videoId;
    }

    private static string? ExtractCanonicalUrl(string html)
    {
        const string marker = "<link rel=\"canonical\" href=\"";

        var idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var start = idx + marker.Length;
        var end = html.IndexOf('"', start);
        if (end <= start)
            return null;

        return html[start..end];
    }

    private static string? ExtractMatchGroup(string input, string pattern)
    {
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value : null;
    }

    public static string NormalizeHandle(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.StartsWith('@') ? trimmed : $"@{trimmed}";
    }

    private static string? TryExtractVideoId(Uri uri)
    {
        // Typical redirect target: /watch?v=<videoId>
        if (!string.Equals(uri.AbsolutePath, "/watch", StringComparison.OrdinalIgnoreCase))
            return null;

        var query = uri.Query;
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var pairs = query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var idx = pair.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = pair[..idx];
            if (!string.Equals(key, "v", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private sealed record HandlePollState(string VideoId, DateTimeOffset NextEligibleAt);
}
