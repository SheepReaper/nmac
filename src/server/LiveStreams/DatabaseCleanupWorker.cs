using Microsoft.EntityFrameworkCore;

using NMAC.Core;

namespace NMAC.LiveStreams;

public partial class DatabaseCleanupWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider tp,
    ILogger<DatabaseCleanupWorker> logger
) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan CompletedSessionRetention = TimeSpan.FromDays(90);

    [LoggerMessage(EventId = 6200, Level = LogLevel.Information, Message = "Database cleanup worker started.")]
    private partial void LogWorkerStarted();

    [LoggerMessage(EventId = 6201, Level = LogLevel.Information, Message = "Database cleanup complete. Expired avatar cache rows removed: {AvatarCount}, old terminal sessions removed: {SessionCount}.")]
    private partial void LogCleanupComplete(int avatarCount, int sessionCount);

    [LoggerMessage(EventId = 6202, Level = LogLevel.Warning, Message = "Database cleanup failed: {Error}")]
    private partial void LogCleanupFailed(string error);

    [LoggerMessage(EventId = 6203, Level = LogLevel.Information, Message = "Database cleanup worker stopped.")]
    private partial void LogWorkerStopped();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted();

        using var timer = new PeriodicTimer(CleanupInterval);

        try
        {
            do
            {
                await RunCleanupAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }

        LogWorkerStopped();
    }

    private async Task RunCleanupAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = tp.GetUtcNow();
            var sessionRetentionCutoff = now - CompletedSessionRetention;

            var avatarCount = await db.AvatarCacheItems
                .Where(a => a.ExpiresAt < now)
                .ExecuteDeleteAsync(ct);

            var sessionCount = await db.LiveChatCaptureSessions
                .Where(s =>
                    (s.State == LiveCaptureSessionState.Completed ||
                     s.State == LiveCaptureSessionState.Failed ||
                     s.State == LiveCaptureSessionState.Cancelled) &&
                    s.CreatedAt < sessionRetentionCutoff)
                .ExecuteDeleteAsync(ct);

            LogCleanupComplete(avatarCount, sessionCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogCleanupFailed(ex.Message);
        }
    }
}
