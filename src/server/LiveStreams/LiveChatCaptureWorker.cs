using Microsoft.EntityFrameworkCore;
using NMAC.Core;

namespace NMAC.LiveStreams;

public partial class LiveChatCaptureWorker(
    IServiceScopeFactory scopeFactory,
    ILiveChatCaptureSignal captureSignal,
    TimeProvider tp,
    ILogger<LiveChatCaptureWorker> logger
) : BackgroundService
{
    private const int MaxConcurrentSessions = 5;
    private static readonly TimeSpan RecoverySweepInterval = TimeSpan.FromSeconds(30);

    [LoggerMessage(EventId = 7101, Level = LogLevel.Information, Message = "Live chat capture worker started.")]
    private partial void LogWorkerStarted();

    [LoggerMessage(EventId = 7102, Level = LogLevel.Information, Message = "Worker is processing session {SessionId} for live chat {LiveChatId}.")]
    private partial void LogProcessingSession(Guid sessionId, string liveChatId);

    [LoggerMessage(EventId = 7103, Level = LogLevel.Error, Message = "Session {SessionId} failed with error: {Error}")]
    private partial void LogSessionFailed(Guid sessionId, string error);

    [LoggerMessage(EventId = 7104, Level = LogLevel.Information, Message = "Session {SessionId} completed successfully.")]
    private partial void LogSessionCompleted(Guid sessionId);

    [LoggerMessage(EventId = 7105, Level = LogLevel.Information, Message = "Reclaimed stale session {SessionId} for live chat {LiveChatId}.")]
    private partial void LogStaleSessionReclaimed(Guid sessionId, string liveChatId);

    [LoggerMessage(EventId = 7106, Level = LogLevel.Information, Message = "Live chat capture worker stopped.")]
    private partial void LogWorkerStopped();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted();
        var activeTasks = new List<Task>(MaxConcurrentSessions);
        var nextRecoverySweep = tp.GetUtcNow();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Retire any completed work so slots free up as soon as possible.
                await DrainCompletedTasksAsync(activeTasks);

                var availableSlots = MaxConcurrentSessions - activeTasks.Count;
                var shouldSweep = tp.GetUtcNow() >= nextRecoverySweep;

                if (availableSlots > 0)
                {
                    var claimedSessions = await ClaimSessionsAsync(availableSlots, shouldSweep, stoppingToken);
                    nextRecoverySweep = tp.GetUtcNow() + RecoverySweepInterval;

                    foreach (var claimed in claimedSessions)
                    {
                        LogProcessingSession(claimed.SessionId, claimed.LiveChatId);

                        if (claimed.ReclaimedStale)
                            LogStaleSessionReclaimed(claimed.SessionId, claimed.LiveChatId);

                        activeTasks.Add(ProcessClaimedSessionAsync(claimed.SessionId, stoppingToken));
                    }

                    // If we just claimed sessions, loop again immediately to fill remaining slots.
                    if (claimedSessions.Count > 0)
                        continue;
                }

                if (activeTasks.Count == 0)
                {
                    // Idle path: react immediately to new enqueues, with periodic recovery sweep fallback.
                    await WaitForSignalOrTimeoutAsync(RecoverySweepInterval, stoppingToken);
                    continue;
                }

                // Active path: wake on either session completion, enqueue signal, or periodic sweep timeout.
                var completionTask = Task.WhenAny(activeTasks);
                var signalTask = captureSignal.WaitAsync(stoppingToken).AsTask();
                var timeoutTask = Task.Delay(RecoverySweepInterval, stoppingToken);
                await Task.WhenAny(completionTask, signalTask, timeoutTask);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown.
        }
        finally
        {
            if (activeTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(activeTasks);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Ignore cancellation during shutdown wait.
                }
            }
        }

        LogWorkerStopped();
    }

    private async Task<IReadOnlyList<ClaimedSession>> ClaimSessionsAsync(int take, bool includeStaleRunning, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var thresholdTime = tp.GetUtcNow().AddMinutes(-2);

        IQueryable<LiveChatCaptureSession> query = db.LiveChatCaptureSessions;

        query = includeStaleRunning
            ? query.Where(s => s.State == LiveCaptureSessionState.Requested || (s.State == LiveCaptureSessionState.Running && (s.LastAttemptAt ?? DateTimeOffset.MinValue) < thresholdTime))
            : query.Where(s => s.State == LiveCaptureSessionState.Requested);

        var candidates = await query
            .OrderBy(s => s.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

        if (candidates.Count == 0)
            return [];

        var now = tp.GetUtcNow();
        var claimed = new List<ClaimedSession>(candidates.Count);

        foreach (var session in candidates)
        {
            var reclaimedStale = session.State == LiveCaptureSessionState.Running;
            session.State = LiveCaptureSessionState.Running;
            session.StartedAt ??= now;
            session.LastAttemptAt = now;
            claimed.Add(new ClaimedSession(session.SessionId, session.LiveChatId, reclaimedStale));
        }

        await db.SaveChangesAsync(ct);

        return claimed;
    }

    private async Task ProcessClaimedSessionAsync(Guid sessionId, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<LiveChatStreamProcessor>();

            var completedSuccessfully = await processor.ProcessSessionAsync(sessionId, ct);
            if (completedSuccessfully)
            {
                LogSessionCompleted(sessionId);
                return;
            }

            if (ct.IsCancellationRequested)
                return;

            LogSessionFailed(sessionId, "Processor returned unsuccessful result.");

            await using var failScope = scopeFactory.CreateAsyncScope();
            var db = failScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.LiveChatCaptureSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

            if (session == null)
                return;

            session.LastError = "Processor returned unsuccessful result.";
            session.RetryCount++;
            session.State = LiveCaptureSessionState.Requested;
            session.LastAttemptAt = tp.GetUtcNow();
            await db.SaveChangesAsync(ct);

            captureSignal.Notify();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Worker is shutting down.
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            LogSessionFailed(sessionId, ex.Message);

            await using var failScope = scopeFactory.CreateAsyncScope();
            var db = failScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.LiveChatCaptureSessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

            if (session == null)
                return;

            // On failure: store the error, increment retry count, reset to Requested for retry.
            session.LastError = ex.Message;
            session.RetryCount++;
            session.State = LiveCaptureSessionState.Requested;
            session.LastAttemptAt = tp.GetUtcNow();
            await db.SaveChangesAsync(ct);

            // Wake worker to retry immediately if a slot is available.
            captureSignal.Notify();
        }
    }

    private static async Task DrainCompletedTasksAsync(List<Task> activeTasks)
    {
        for (var i = activeTasks.Count - 1; i >= 0; i--)
        {
            var task = activeTasks[i];
            if (!task.IsCompleted)
                continue;

            activeTasks.RemoveAt(i);
            await task;
        }
    }

    private async Task WaitForSignalOrTimeoutAsync(TimeSpan timeout, CancellationToken ct)
    {
        var signalTask = captureSignal.WaitAsync(ct).AsTask();
        var timeoutTask = Task.Delay(timeout, ct);
        await Task.WhenAny(signalTask, timeoutTask);
    }

    private sealed record ClaimedSession(Guid SessionId, string LiveChatId, bool ReclaimedStale);
}
