using System.Diagnostics.Metrics;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Grpc.Core;

using NMAC.Core;

using ProtoBuf.Grpc;

using youtube.api.v3;

namespace NMAC.LiveStreams;

public partial class LiveChatStreamProcessor(
    IYouTubeLiveChatStreamList liveChat,
    IServiceScopeFactory scopeFactory,
    ILiveStreamUpdatePublisher updatePublisher,
    IOptions<YTGrpcClientOptions> grpcOptions,
    TimeProvider tp,
    ILogger<LiveChatStreamProcessor> logger
)
{
    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Persisted {Count} SuperChat(s) for video {VideoId}.")]
    private partial void LogSuperChatsBatchPersisted(string videoId, int count);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Warning, Message = "SuperChat batch for video {VideoId} was queued but SaveChanges affected 0 rows. BatchCount={BatchCount}.")]
    private partial void LogSuperChatsBatchNoRowsAffected(string videoId, int batchCount);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Information, Message = "Skipped {DuplicateCount} duplicate SuperChat(s) for video {VideoId} before insert.")]
    private partial void LogSuperChatsDuplicatesSkipped(string videoId, int duplicateCount);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Information, Message = "Persisted {Count} funding donation(s) for video {VideoId}.")]
    private partial void LogFundingDonationsBatchPersisted(string videoId, int count);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Warning, Message = "Funding donation batch for video {VideoId} was queued but SaveChanges affected 0 rows. BatchCount={BatchCount}.")]
    private partial void LogFundingDonationsBatchNoRowsAffected(string videoId, int batchCount);

    [LoggerMessage(EventId = 7006, Level = LogLevel.Information, Message = "Skipped {DuplicateCount} duplicate funding donation(s) for video {VideoId} before insert.")]
    private partial void LogFundingDonationsDuplicatesSkipped(string videoId, int duplicateCount);

    [LoggerMessage(EventId = 7007, Level = LogLevel.Information, Message = "Stream for video {VideoId} went offline at {OfflineAt}.")]
    private partial void LogStreamOffline(string videoId, string offlineAt);

    [LoggerMessage(EventId = 7008, Level = LogLevel.Information, Message = "Chat ended for video {VideoId}, live chat {LiveChatId}.")]
    private partial void LogChatEnded(string videoId, string liveChatId);

    [LoggerMessage(EventId = 7009, Level = LogLevel.Warning, Message = "gRPC error for video {VideoId}: {StatusCode} — {Detail}")]
    private partial void LogRpcError(string videoId, StatusCode statusCode, string detail);

    [LoggerMessage(EventId = 7010, Level = LogLevel.Error, Message = "Gave up on video {VideoId} after {MaxRetries} retry attempt(s): {LastError}")]
    private partial void LogMaxRetriesExceeded(string videoId, int maxRetries, string lastError);

    [LoggerMessage(EventId = 7011, Level = LogLevel.Information, Message = "Stopped listening to live chat {LiveChatId} for video {VideoId}. CompletionStatus={CompletionStatus}.")]
    private partial void LogStreamCompleted(string videoId, string liveChatId, string completionStatus);

    [LoggerMessage(EventId = 7012, Level = LogLevel.Information, Message = "Reconnect delay for video {VideoId}: {DelayMs}ms ({Reason}).")]
    private partial void LogReconnectDelayChosen(string videoId, int delayMs, string reason);

    [LoggerMessage(EventId = 7013, Level = LogLevel.Information, Message = "Stream connection for video {VideoId} ended: outcome={Outcome}, duration={DurationSeconds}s, responses={Responses}, hasResumeToken={HasResumeToken}.")]
    private partial void LogConnectionEnded(string videoId, string outcome, double durationSeconds, int responses, bool hasResumeToken);

    [LoggerMessage(EventId = 7014, Level = LogLevel.Information, Message = "Session telemetry for video {VideoId}: outcome={Outcome}, connections={Connections}, rollovers={Rollovers}, rpcReconnects={RpcReconnects}, responses={Responses}, superChatsPersisted={SuperChatsPersisted}, estimatedQuotaUnits={EstimatedQuotaUnits}.")]
    private partial void LogSessionTelemetry(string videoId, string outcome, int connections, int rollovers, int rpcReconnects, int responses, int superChatsPersisted, int estimatedQuotaUnits);

    [LoggerMessage(EventId = 7015, Level = LogLevel.Warning, Message = "Live chat capture session {SessionId} was not found. Skipping processing.")]
    private partial void LogSessionNotFound(Guid sessionId);

    [LoggerMessage(EventId = 7016, Level = LogLevel.Error, Message = "Live chat stream failed for session {SessionId}. Returning without throw.")]
    private partial void LogSessionFailed(Guid sessionId, Exception exception);

    [LoggerMessage(EventId = 7017, Level = LogLevel.Information, Message = "Treating gRPC status {StatusCode} for video {VideoId} as terminal live-chat-unavailable.")]
    private partial void LogTerminalRpcUnavailable(string videoId, StatusCode statusCode);

    private readonly string ApiKey = grpcOptions.Value.ApiKey;

    private const int StreamListCallCostUnits = 5;
    private const int ServerBufferLimit = 100;
    private static readonly TimeSpan MaxPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MinPollInterval = TimeSpan.FromMilliseconds(200);
    private const double ReconnectHeadroomRatio = 0.6;

    private static readonly Meter LiveChatMeter = new(Telemetry.MeterName);
    private static readonly Counter<long> ConnectionAttemptsCounter =
        LiveChatMeter.CreateCounter<long>("NMAC.livechat.connection_attempts");
    private static readonly Counter<long> ReconnectsCounter =
        LiveChatMeter.CreateCounter<long>("NMAC.livechat.reconnects");
    private static readonly Histogram<double> ConnectionDurationSeconds =
        LiveChatMeter.CreateHistogram<double>("NMAC.livechat.connection_duration_seconds");
    private static readonly Counter<long> SessionEstimatedQuotaUnitsCounter =
        LiveChatMeter.CreateCounter<long>("NMAC.livechat.session_estimated_quota_units");

    private static readonly LiveChatMessageSnippet.TypeWrapper.Type SuperChatEvent =
        LiveChatMessageSnippet.TypeWrapper.Type.SuperChatEvent;

    private static readonly LiveChatMessageSnippet.TypeWrapper.Type FanFundingEvent =
        LiveChatMessageSnippet.TypeWrapper.Type.FanFundingEvent;

    private static readonly LiveChatMessageSnippet.TypeWrapper.Type ChatEndedEvent =
        LiveChatMessageSnippet.TypeWrapper.Type.ChatEndedEvent;

    /// <summary>
    /// Processes a live chat capture session by streaming messages from YouTube and persisting SuperChats.
    /// Uses fresh DbContext scopes for each batch insert and final state update to support multi-hour streams.
    /// </summary>
    public async Task<bool> ProcessSessionAsync(Guid sessionId, CancellationToken ct)
    {
        // Load the session record once at the start to get VideoId and LiveChatId
        LiveChatCaptureSession? session;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            session = await db.LiveChatCaptureSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        }

        if (session == null)
        {
            LogSessionNotFound(sessionId);
            return true;
        }

        Metadata headers = new() { { "x-goog-api-key", ApiKey } };

        var request = new LiveChatMessageListRequest { LiveChatId = session.LiveChatId, MaxResults = 2000 };
        request.Parts.AddRange(["snippet", "authorDetails"]);

        CallContext ctx = new CallOptions(headers, cancellationToken: ct);

        const int maxRetries = 3;
        int retryCount = 0;
        string? nextPageToken = null;
        DateTimeOffset? lastMessageAt = null;
        var terminalSessionOutcome = LiveChatCompletionStatus.Continue;
        Exception? terminalFailure = null;
        DateTimeOffset? previousConnectionStartedAt = null;
        var totalConnectionAttempts = 0;
        var rolloverReconnects = 0;
        var rpcReconnects = 0;
        var totalResponses = 0;
        var totalSuperChatsPersisted = 0;
        var totalFundingDonationsPersisted = 0;

        void EmitSessionTelemetry(string outcome)
        {
            var estimatedQuotaUnits = totalConnectionAttempts * StreamListCallCostUnits;
            LogSessionTelemetry(
                session.VideoId,
                outcome,
                totalConnectionAttempts,
                rolloverReconnects,
                rpcReconnects,
                totalResponses,
                totalSuperChatsPersisted,
                estimatedQuotaUnits
            );
            // totalFundingDonationsPersisted is available for future telemetry expansion

            SessionEstimatedQuotaUnitsCounter.Add(
                estimatedQuotaUnits,
                new KeyValuePair<string, object?>("outcome", outcome)
            );
        }

        while (!ct.IsCancellationRequested)
        {
            var connectionStartedAt = tp.GetUtcNow();
            var connectionAttemptInterval = previousConnectionStartedAt.HasValue
                ? connectionStartedAt - previousConnectionStartedAt.Value
                : (TimeSpan?)null;
            previousConnectionStartedAt = connectionStartedAt;
            totalConnectionAttempts++;

            ConnectionAttemptsCounter.Add(1);

            var attempt = await RunConnectionAttemptAsync(session, request, ctx, nextPageToken, ct);

            nextPageToken = attempt.NextPageToken;
            totalResponses += attempt.ResponseCount;
            totalSuperChatsPersisted += attempt.SuperChatsPersisted;
            totalFundingDonationsPersisted += attempt.FundingDonationsPersisted;
            if (attempt.RpcException is not null)
                rpcReconnects++;

            if (attempt.LastMessageAt.HasValue)
                lastMessageAt = attempt.LastMessageAt;

            if (attempt.CompletionStatus == LiveChatCompletionStatus.Cancelled)
                break;

            if (attempt.RpcException is not null)
            {
                retryCount++;

                if (retryCount > maxRetries)
                {
                    LogMaxRetriesExceeded(session.VideoId, maxRetries, $"{attempt.RpcException.StatusCode}: {attempt.RpcException.Status.Detail}");
                    terminalFailure = attempt.RpcException;
                    break;
                }

                var backoff = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                ReconnectsCounter.Add(1, new KeyValuePair<string, object?>("reason", "rpc-retry-backoff"));
                LogReconnectDelayChosen(session.VideoId, (int)backoff.TotalMilliseconds, "rpc-retry-backoff");
                await Task.Delay(backoff, ct);
                continue;
            }

            retryCount = 0;

            if (attempt.CompletionStatus != LiveChatCompletionStatus.Continue)
            {
                if (attempt.CompletionStatus == LiveChatCompletionStatus.ChatEnded)
                    LogChatEnded(session.VideoId, session.LiveChatId);

                terminalSessionOutcome = attempt.CompletionStatus;
                break;
            }

            // Stream ended without a graceful termination signal — pause before reconnecting.
            rolloverReconnects++;

            var instantaneousRate = CalculateInstantaneousMessageRate(
                attempt.MessageCount,
                attempt.OldestMessagePublishedAt,
                attempt.NewestMessagePublishedAt,
                connectionAttemptInterval
            );

            var reconnectDelay = CalculateAdaptiveReconnectDelay(instantaneousRate);

            ReconnectsCounter.Add(1, new KeyValuePair<string, object?>("reason", "stream-rollover"));
            await Task.Delay(reconnectDelay, ct);
        }

        if (ct.IsCancellationRequested)
        {
            LogStreamCompleted(session.VideoId, session.LiveChatId, LiveChatCompletionStatus.Cancelled);
            EmitSessionTelemetry(LiveChatCompletionStatus.Cancelled);
            return false;
        }

        if (terminalFailure is not null)
        {
            LogStreamCompleted(session.VideoId, session.LiveChatId, LiveChatCompletionStatus.Failed);
            EmitSessionTelemetry(LiveChatCompletionStatus.Failed);
            LogSessionFailed(sessionId, terminalFailure);
            return false;
        }

        // Create a final fresh scope to update and persist session state
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var record = await db.LiveChatCaptureSessions
                .FirstAsync(s => s.SessionId == sessionId, ct);

            // Update session state to completed
            record.State = "Completed";
            record.CompletedAt = tp.GetUtcNow();
            if (lastMessageAt.HasValue)
                record.LastMessageAt = lastMessageAt.Value;

            await db.SaveChangesAsync(ct);
        }

        LogStreamCompleted(session.VideoId, session.LiveChatId, terminalSessionOutcome);
        EmitSessionTelemetry(terminalSessionOutcome);
        return true;
    }

    private async Task<ConnectionAttemptResult> RunConnectionAttemptAsync(
        LiveChatCaptureSession session,
        LiveChatMessageListRequest request,
        CallContext ctx,
        string? currentPageToken,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(currentPageToken))
            request.PageToken = currentPageToken;

        var connectionOutcome = "stream-rollover";
        var responseCount = 0;
        var messageCount = 0;
        var superChatsPersisted = 0;
        var fundingDonationsPersisted = 0;
        DateTimeOffset? oldestMessagePublishedAt = null;
        DateTimeOffset? newestMessagePublishedAt = null;
        DateTimeOffset? lastMessageAt = null;
        var completionStatus = LiveChatCompletionStatus.Continue;
        RpcException? rpcException = null;
        var nextPageToken = currentPageToken;
        var connectionStartedAt = tp.GetUtcNow();

        try
        {
            await foreach (var response in liveChat.StreamList(request, ctx))
            {
                responseCount++;

                if (!string.IsNullOrEmpty(response.NextPageToken))
                    nextPageToken = response.NextPageToken;

                if (!string.IsNullOrEmpty(response.OfflineAt))
                {
                    LogStreamOffline(session.VideoId, response.OfflineAt);
                    connectionOutcome = "offline";
                    completionStatus = LiveChatCompletionStatus.Offline;
                    break;
                }

                if (response.Items is null or { Count: 0 })
                    continue;

                var batch = AnalyzeResponseItems(response.Items, session);
                messageCount += batch.MessageCount;
                if (batch.OldestMessagePublishedAt.HasValue)
                    oldestMessagePublishedAt = MinTimestamp(oldestMessagePublishedAt, batch.OldestMessagePublishedAt.Value);

                if (batch.NewestMessagePublishedAt.HasValue)
                    newestMessagePublishedAt = MaxTimestamp(newestMessagePublishedAt, batch.NewestMessagePublishedAt.Value);

                if (batch.SuperChats.Count > 0)
                {
                    var rowsAffected = await PersistSuperChatsBatchAsync(session.VideoId, batch.SuperChats, ct);
                    superChatsPersisted += rowsAffected;

                    if (rowsAffected > 0)
                    {
                        LogSuperChatsBatchPersisted(session.VideoId, rowsAffected);
                        updatePublisher.PublishSuperChatsPersisted(session.VideoId, rowsAffected);
                    }
                    else
                        LogSuperChatsBatchNoRowsAffected(session.VideoId, batch.SuperChats.Count);
                }

                if (batch.FundingDonations.Count > 0)
                {
                    var rowsAffected = await PersistFundingDonationsBatchAsync(session.VideoId, batch.FundingDonations, ct);
                    fundingDonationsPersisted += rowsAffected;

                    if (rowsAffected > 0)
                        LogFundingDonationsBatchPersisted(session.VideoId, rowsAffected);
                    else
                        LogFundingDonationsBatchNoRowsAffected(session.VideoId, batch.FundingDonations.Count);
                }

                // Track the latest message time in memory; only write it at final state update.
                lastMessageAt = tp.GetUtcNow();

                if (batch.ChatEnded)
                {
                    connectionOutcome = "chat-ended";
                    completionStatus = LiveChatCompletionStatus.ChatEnded;
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            connectionOutcome = "cancelled";
            completionStatus = LiveChatCompletionStatus.Cancelled;
        }
        catch (RpcException ex) when (!ct.IsCancellationRequested)
        {
            if (IsTerminalUnavailableStatus(ex.StatusCode))
            {
                connectionOutcome = $"rpc-terminal-{ex.StatusCode}";
                LogTerminalRpcUnavailable(session.VideoId, ex.StatusCode);
                completionStatus = LiveChatCompletionStatus.Offline;
            }
            else
            {
                connectionOutcome = $"rpc-{ex.StatusCode}";
                LogRpcError(session.VideoId, ex.StatusCode, ex.Status.Detail);
                rpcException = ex;
            }
        }
        finally
        {
            var connectionDurationSeconds = (tp.GetUtcNow() - connectionStartedAt).TotalSeconds;
            ConnectionDurationSeconds.Record(
                connectionDurationSeconds,
                new KeyValuePair<string, object?>("outcome", connectionOutcome)
            );
            LogConnectionEnded(
                session.VideoId,
                connectionOutcome,
                Math.Round(connectionDurationSeconds, 3),
                responseCount,
                !string.IsNullOrEmpty(nextPageToken)
            );
        }

        return new ConnectionAttemptResult(
            completionStatus,
            rpcException,
            nextPageToken,
            responseCount,
            messageCount,
            superChatsPersisted,
            fundingDonationsPersisted,
            oldestMessagePublishedAt,
            newestMessagePublishedAt,
            lastMessageAt
        );
    }

    private static ResponseBatchResult AnalyzeResponseItems(IEnumerable<LiveChatMessage> items, LiveChatCaptureSession session)
    {
        var superChats = new List<LiveSuperChat>();
        var fundingDonations = new List<LiveFundingDonation>();
        var messageCount = 0;
        var chatEnded = false;
        DateTimeOffset? oldestMessagePublishedAt = null;
        DateTimeOffset? newestMessagePublishedAt = null;

        foreach (var item in items)
        {
            messageCount++;

            var snippetType = item.Snippet?.Type;
            if (snippetType == ChatEndedEvent)
                chatEnded = true;

            if (snippetType == SuperChatEvent && !string.IsNullOrEmpty(item.Id))
                superChats.Add(MapToEntity(item, session));

            if (snippetType == FanFundingEvent && !string.IsNullOrEmpty(item.Id))
                fundingDonations.Add(MapToFundingDonationEntity(item, session));

            if (string.IsNullOrEmpty(item.Snippet?.PublishedAt)
                || !DateTimeOffset.TryParse(item.Snippet.PublishedAt, out var publishedAt))
            {
                continue;
            }

            oldestMessagePublishedAt = MinTimestamp(oldestMessagePublishedAt, publishedAt);
            newestMessagePublishedAt = MaxTimestamp(newestMessagePublishedAt, publishedAt);
        }

        return new ResponseBatchResult(
            superChats,
            fundingDonations,
            messageCount,
            chatEnded,
            oldestMessagePublishedAt,
            newestMessagePublishedAt
        );
    }

    private async Task<int> PersistFundingDonationsBatchAsync(string videoId, List<LiveFundingDonation> batch, CancellationToken ct)
    {
        var uniqueBatch = batch
            .GroupBy(d => d.MessageId, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rowsAffected = await db.ExecuteBatchInsertAsync(uniqueBatch, ct);
        var duplicateCount = batch.Count - rowsAffected;
        if (duplicateCount > 0)
            LogFundingDonationsDuplicatesSkipped(videoId, duplicateCount);

        return rowsAffected;
    }

    private async Task<int> PersistSuperChatsBatchAsync(string videoId, List<LiveSuperChat> batch, CancellationToken ct)
    {
        var uniqueBatch = batch
            .GroupBy(c => c.MessageId, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rowsAffected = await db.ExecuteBatchInsertAsync(uniqueBatch, ct);
        var duplicateCount = batch.Count - rowsAffected;
        if (duplicateCount > 0)
            LogSuperChatsDuplicatesSkipped(videoId, duplicateCount);

        return rowsAffected;
    }

    private static DateTimeOffset? MinTimestamp(DateTimeOffset? current, DateTimeOffset candidate) =>
        !current.HasValue || candidate < current.Value ? candidate : current;

    private static DateTimeOffset? MaxTimestamp(DateTimeOffset? current, DateTimeOffset candidate) =>
        !current.HasValue || candidate > current.Value ? candidate : current;

    private sealed record ResponseBatchResult(
        List<LiveSuperChat> SuperChats,
        List<LiveFundingDonation> FundingDonations,
        int MessageCount,
        bool ChatEnded,
        DateTimeOffset? OldestMessagePublishedAt,
        DateTimeOffset? NewestMessagePublishedAt
    );

    private sealed record ConnectionAttemptResult(
        LiveChatCompletionStatus CompletionStatus,
        RpcException? RpcException,
        string? NextPageToken,
        int ResponseCount,
        int MessageCount,
        int SuperChatsPersisted,
        int FundingDonationsPersisted,
        DateTimeOffset? OldestMessagePublishedAt,
        DateTimeOffset? NewestMessagePublishedAt,
        DateTimeOffset? LastMessageAt
    );

    private static double CalculateInstantaneousMessageRate(
        int connectionMessageCount,
        DateTimeOffset? oldestMessagePublishedAt,
        DateTimeOffset? newestMessagePublishedAt,
        TimeSpan? connectionAttemptInterval)
    {
        if (connectionMessageCount <= 0)
            return 0;

        if (oldestMessagePublishedAt.HasValue
            && newestMessagePublishedAt.HasValue
            && newestMessagePublishedAt.Value > oldestMessagePublishedAt.Value)
        {
            var windowSeconds = (newestMessagePublishedAt.Value - oldestMessagePublishedAt.Value).TotalSeconds;
            if (windowSeconds > 0)
                return connectionMessageCount / windowSeconds;
        }

        if (connectionAttemptInterval.HasValue && connectionAttemptInterval.Value > TimeSpan.Zero)
        {
            return connectionMessageCount / connectionAttemptInterval.Value.TotalSeconds;
        }

        return 0;
    }

    private static TimeSpan CalculateAdaptiveReconnectDelay(double instantaneousRate)
    {
        if (instantaneousRate <= 0)
            return MaxPollInterval;

        var safeGapSeconds = ServerBufferLimit * ReconnectHeadroomRatio / instantaneousRate;
        var candidate = TimeSpan.FromSeconds(safeGapSeconds);

        if (candidate < MinPollInterval)
            return MinPollInterval;

        if (candidate > MaxPollInterval)
            return MaxPollInterval;

        return candidate;
    }

    private static bool IsTerminalUnavailableStatus(StatusCode statusCode)
        => statusCode is StatusCode.FailedPrecondition or StatusCode.NotFound;

    private static LiveSuperChat MapToEntity(LiveChatMessage msg, LiveChatCaptureSession session)
    {
        var sc = msg.Snippet?.SuperChatDetails;
        var author = msg.AuthorDetails;

        DateTimeOffset? publishedAt = null;
        if (!string.IsNullOrEmpty(msg.Snippet?.PublishedAt)
            && DateTimeOffset.TryParse(msg.Snippet.PublishedAt, out var dt))
            publishedAt = dt;

        return new LiveSuperChat
        {
            MessageId = msg.Id!,
            VideoId = session.VideoId,
            LiveChatId = session.LiveChatId,
            AuthorChannelId = author?.ChannelId,
            AuthorDisplayName = author?.DisplayName,
            AuthorProfileImageUrl = author?.ProfileImageUrl,
            AmountMicros = (long)(sc?.AmountMicros ?? 0UL),
            Currency = sc?.Currency,
            AmountDisplayString = sc?.AmountDisplayString,
            MessageContent = sc?.UserComment,
            PublishedAt = publishedAt
        };
    }

    private static LiveFundingDonation MapToFundingDonationEntity(LiveChatMessage msg, LiveChatCaptureSession session)
    {
        var ff = msg.Snippet?.FanFundingDetails;
        var author = msg.AuthorDetails;

        DateTimeOffset? publishedAt = null;
        if (!string.IsNullOrEmpty(msg.Snippet?.PublishedAt)
            && DateTimeOffset.TryParse(msg.Snippet.PublishedAt, out var dt))
            publishedAt = dt;

        return new LiveFundingDonation
        {
            MessageId = msg.Id!,
            VideoId = session.VideoId,
            LiveChatId = session.LiveChatId,
            AuthorChannelId = author?.ChannelId,
            AuthorDisplayName = author?.DisplayName,
            AmountMicros = (long)(ff?.AmountMicros ?? 0UL),
            Currency = ff?.Currency,
            AmountDisplayString = ff?.AmountDisplayString,
            UserComment = ff?.UserComment,
            PublishedAt = publishedAt
        };
    }
}
