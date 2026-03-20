using NMAC.Events;
using NMAC.Subscriptions.WebSub;

namespace NMAC.Subscriptions;

public partial class SubscribeToChannelHandler(
    SubscriptionService subscriptionsService,
    ILogger<SubscribeToChannelHandler> logger
)
{
    [LoggerMessage(EventId = 3101, Level = LogLevel.Information, Message = "Attempting to subscribe to channel {ChannelId}.")]
    private partial void LogSubscribingToChannel(string channelId);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information, Message = "Successfully subscribed to channel {ChannelId}.")]
    private partial void LogSubscribedToChannel(string channelId);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Error, Message = "Failed to subscribe to channel {ChannelId}.")]
    private partial void LogSubscribeToChannelFailed(string channelId);

    public async Task HandleAsync(SubscribeToChannel command, CancellationToken ct)
    {
        LogSubscribingToChannel(command.ChannelId);

        Subscription sub = new()
        {
            Enabled = true,
            Mode = HubMode.Subscribe,
            TopicUri = new Uri(string.Format(SubscriptionService.ChannelTopicTemplate, command.ChannelId))
        };

        if (!await subscriptionsService.TryNewSubscribeAsync(sub, false, ct))
        {
            LogSubscribeToChannelFailed(command.ChannelId);
            return;
        }

        LogSubscribedToChannel(command.ChannelId);
    }
}