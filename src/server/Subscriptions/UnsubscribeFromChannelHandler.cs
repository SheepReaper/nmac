using Microsoft.EntityFrameworkCore;

using NMAC.Core;
using NMAC.Events;

namespace NMAC.Subscriptions;

public partial class UnsubscribeFromChannelHandler(
    AppDbContext db,
    SubscriptionService subscriptionsService,
    ILogger<UnsubscribeFromChannelHandler> logger)
{
    [LoggerMessage(EventId = 3111, Level = LogLevel.Information, Message = "Attempting to unsubscribe channel {ChannelId}.")]
    private partial void LogUnsubscribingChannel(string channelId);

    [LoggerMessage(EventId = 3112, Level = LogLevel.Warning, Message = "No active subscription found for channel {ChannelId}.")]
    private partial void LogSubscriptionNotFound(string channelId);

    [LoggerMessage(EventId = 3113, Level = LogLevel.Error, Message = "Subscription for channel {ChannelId} has no callback URI and cannot be unsubscribed via hub.")]
    private partial void LogUnsubscribeMissingCallback(string channelId);

    [LoggerMessage(EventId = 3114, Level = LogLevel.Error, Message = "Failed to unsubscribe channel {ChannelId}.")]
    private partial void LogUnsubscribeFailed(string channelId);

    [LoggerMessage(EventId = 3115, Level = LogLevel.Information, Message = "Successfully requested unsubscription for channel {ChannelId}.")]
    private partial void LogUnsubscribedChannel(string channelId);

    public async Task Handle(UnsubscribeFromChannel command, CancellationToken ct)
    {
        LogUnsubscribingChannel(command.ChannelId);

        var topicUri = new Uri(string.Format(SubscriptionService.ChannelTopicTemplate, command.ChannelId));
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(s => s.TopicUri == topicUri, ct);

        if (subscription is null)
        {
            LogSubscriptionNotFound(command.ChannelId);
            return;
        }

        subscription.Enabled = false;
        await db.SaveChangesAsync(ct);

        if (subscription.CallbackUri is null)
        {
            LogUnsubscribeMissingCallback(command.ChannelId);
            return;
        }

        if (!await subscriptionsService.TryUnsubscribeAsync(subscription, ct))
        {
            LogUnsubscribeFailed(command.ChannelId);
            return;
        }

        LogUnsubscribedChannel(command.ChannelId);
    }
}
