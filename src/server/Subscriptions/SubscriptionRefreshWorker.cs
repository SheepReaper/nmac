namespace NMAC.Subscriptions;

public partial class SubscriptionRefreshWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionRefreshWorker> logger) : BackgroundService
{
    /// <summary>
    /// How often to check for expiring subscriptions. Leases are 10 days,
    /// so daily checks with a 2-day refresh threshold ensure no subscription
    /// expires before being renewed.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    /// <summary>
    /// Refresh any subscription whose expiration is within this window.
    /// </summary>
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromDays(2);

    [LoggerMessage(EventId = 3201, Level = LogLevel.Information, Message = "Subscription refresh worker started. CheckInterval={CheckInterval}, RefreshThreshold={RefreshThreshold}.")]
    private partial void LogWorkerStarted(TimeSpan checkInterval, TimeSpan refreshThreshold);

    [LoggerMessage(EventId = 3202, Level = LogLevel.Information, Message = "Subscription refresh cycle beginning.")]
    private partial void LogCycleStarting();

    [LoggerMessage(EventId = 3203, Level = LogLevel.Error, Message = "Unhandled error during subscription refresh cycle.")]
    private partial void LogCycleFailed(Exception ex);

    [LoggerMessage(EventId = 3204, Level = LogLevel.Information, Message = "Subscription refresh worker stopped.")]
    private partial void LogWorkerStopped();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogWorkerStarted(CheckInterval, RefreshThreshold);

        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                LogCycleStarting();

                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var subs = scope.ServiceProvider.GetRequiredService<SubscriptionService>();
                    await subs.RefreshExpiringSubscriptionsAsync(RefreshThreshold, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogCycleFailed(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path — swallow.
        }

        LogWorkerStopped();
    }
}
