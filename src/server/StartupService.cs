namespace NMAC;

public partial class StartupService(
    ILogger<StartupService> logger) : BackgroundService
{
    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "{ServiceName} is running.")]
    partial void LogServiceRunning(string ServiceName = nameof(StartupService));

    [LoggerMessage(EventId = 9002, Level = LogLevel.Information, Message = "{ServiceName} has completed.")]
    partial void LogServiceCompleted(string ServiceName = nameof(StartupService));

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceRunning();
        LogServiceCompleted();

        return Task.CompletedTask;
    }
}
