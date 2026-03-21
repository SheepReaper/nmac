using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NMAC.Core;
using NMAC.Subscriptions.WebSub;

namespace NMAC.Subscriptions;

public partial class SubscriptionService(
    IOptions<SubscriptionServiceOptions> options,
    AppDbContext db,
    WebSubClient ws,
    TimeProvider tp,
    ILogger<SubscriptionService> logger
    )
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Subscription to topic {TopicUri} has expired. Attempting to resubscribe.")]
    private partial void LogSubscriptionExpired(Uri topicUri);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "Failed to resubscribe to topic {TopicUri}.")]
    private partial void LogResubscribeFailed(Uri topicUri);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Information, Message = "Subscriptions refresh completed.")]
    private partial void LogSubscriptionsRefreshCompleted();

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information, Message = "Subscription to topic {TopicUri} is disabled. Attempting to unsubscribe.")]
    private partial void LogDisabledSubscriptionUnsubscribing(Uri topicUri);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Error, Message = "Failed to unsubscribe from topic {TopicUri}.")]
    private partial void LogUnsubscribeFailed(Uri topicUri);

    [LoggerMessage(EventId = 3006, Level = LogLevel.Information, Message = "Disabled subscriptions cleanup completed.")]
    private partial void LogDisabledSubscriptionsCleanupCompleted();

    [LoggerMessage(EventId = 3007, Level = LogLevel.Information, Message = "Attempting to resubscribe to topic {TopicUri}.")]
    private partial void LogResubscribing(Uri topicUri);

    [LoggerMessage(EventId = 3008, Level = LogLevel.Error, Message = "Failed to resubscribe to topic {TopicUri}.")]
    private partial void LogResubscribeAllFailed(Uri topicUri);

    [LoggerMessage(EventId = 3009, Level = LogLevel.Information, Message = "All subscriptions resubscribe attempt completed.")]
    private partial void LogResubscribeAllCompleted();

    [LoggerMessage(EventId = 3010, Level = LogLevel.Information, Message = "Attempting to subscribe to topic {TopicUri} for pending subscription.")]
    private partial void LogSubscribingPending(Uri topicUri);

    [LoggerMessage(EventId = 3011, Level = LogLevel.Error, Message = "Failed to subscribe to topic {TopicUri} for pending subscription.")]
    private partial void LogSubscribePendingFailed(Uri topicUri);

    [LoggerMessage(EventId = 3012, Level = LogLevel.Information, Message = "Removed {Count} broken subscriptions without callback URIs.")]
    private partial void LogBrokenSubscriptionsRemoved(int count);

    [LoggerMessage(EventId = 3013, Level = LogLevel.Information, Message = "Archived {Count} subscriptions to orphaned table.")]
    private partial void LogSubscriptionsArchived(int count);

    [LoggerMessage(EventId = 3014, Level = LogLevel.Information, Message = "Attempting to unsubscribe orphaned callback {CallbackUri}.")]
    private partial void LogUnsubscribingOrphan(Uri callbackUri);

    [LoggerMessage(EventId = 3015, Level = LogLevel.Warning, Message = "Failed to unsubscribe orphaned callback {CallbackUri}.")]
    private partial void LogOrphanUnsubscribeFailed(Uri callbackUri);

    [LoggerMessage(EventId = 3016, Level = LogLevel.Information, Message = "Persisted pending subscription for topic {TopicUri}. Slug: {Slug}, Callback: {CallbackUri}.")]
    private partial void LogPendingSubscriptionPersisted(Uri topicUri, Guid slug, Uri callbackUri);

    private readonly Uri CallbackBaseUri = options.Value.CallbackBaseUri;
    private readonly Uri HubUri = options.Value.HubUri;

    public const string ChannelTopicTemplate = "http://www.youtube.com/feeds/videos.xml?channel_id={0}";

    public Task<bool> TryResubscribeAsync(Subscription subscription, bool insecure = false, CancellationToken stoppingToken = default)
    {
        var request = insecure ? subscription.ResubWithoutSecret() : subscription.Resub();

        return TryWebSubAsync(request, insecure, stoppingToken);
    }

    public Task<bool> TryNewSubscribeAsync(Subscription subscription, bool insecure = false, CancellationToken stoppingToken = default)
    {
        Uri callbackUri = new(CallbackBaseUri, Guid.NewGuid().ToString());

        var request = insecure
            ? subscription.ResubWithoutSecret(callbackUri)
            : subscription.Resub(callbackUri);

        return TryWebSubAsync(request, insecure, stoppingToken);
    }

    public Task<bool> TryUnsubscribeAsync(Subscription subscription, CancellationToken stoppingToken = default)
    {
        var request = subscription.ResubWithoutSecret(mode: HubMode.Unsubscribe);

        return TryWebSubAsync(request, true, stoppingToken);
    }

    private async Task<bool> TryWebSubAsync(
        SubscriptionRequest request,
        bool insecure = false,
        CancellationToken stoppingToken = default
        )
    {
        Subscription pending = new()
        {
            CallbackUri = request.CallbackUri,
            Mode = request.Mode,
            TopicUri = request.TopicUri,
            Secret = request.Secret,
            Slug = request.ExtractGuidSlug(),
            Enabled = request.Mode != HubMode.Unsubscribe
        };

        await db.ExecuteUpsertAsync(pending, stoppingToken);
        LogPendingSubscriptionPersisted(pending.TopicUri, pending.Slug!.Value, pending.CallbackUri!);

        var (success, migratedTopicUri) = await ws.TryWebSubAsync(HubUri, request, insecure, stoppingToken);

        if (!success)
        {
            await db.Subscriptions.Where(s => s.TopicUri == request.TopicUri).ExecuteUpdateAsync((e) => e
                .SetProperty(s => s.Secret, (_) => null)
                .SetProperty(s => s.Slug, (_) => null)
                .SetProperty(s => s.Mode, (_) => null)
                .SetProperty(s => s.Expiration, (_) => null)
                .SetProperty(s => s.CallbackUri, (_) => null)
            , stoppingToken);

            return false;
        }

        return true;
    }

    public async Task RefreshExpiringSubscriptionsAsync(TimeSpan? threshold = null, CancellationToken stoppingToken = default)
    {
        var effectiveThreshold = threshold ?? TimeSpan.FromDays(2);
        var query = db.Subscriptions.Where(s => s.Enabled && s.CallbackUri != null && s.Expiration.HasValue && s.Expiration.Value < tp.GetUtcNow() + effectiveThreshold);

        await foreach (var subscription in query.AsAsyncEnumerable().WithCancellation(stoppingToken))
        {
            LogSubscriptionExpired(subscription.TopicUri);

            if (!await TryResubscribeAsync(subscription, false, stoppingToken))
                LogResubscribeFailed(subscription.TopicUri);
        }

        LogSubscriptionsRefreshCompleted();
    }

    public async Task UnsubscribeDisabledSubscriptionsAsync(CancellationToken stoppingToken = default)
    {
        var query = db.Subscriptions.Where(s => !s.Enabled && s.Expiration.HasValue && s.CallbackUri != null);

        await foreach (var subscription in query.AsAsyncEnumerable().WithCancellation(stoppingToken))
        {
            LogDisabledSubscriptionUnsubscribing(subscription.TopicUri);

            if (!await TryUnsubscribeAsync(subscription, stoppingToken))
                LogUnsubscribeFailed(subscription.TopicUri);
        }

        LogDisabledSubscriptionsCleanupCompleted();
    }

    public async Task ResubscribeAllAsync(CancellationToken stoppingToken = default)
    {
        var query = db.Subscriptions.Where(s => s.Enabled && s.CallbackUri != null);

        await foreach (var subscription in query.AsAsyncEnumerable().WithCancellation(stoppingToken))
        {
            LogResubscribing(subscription.TopicUri);

            if (!await TryNewSubscribeAsync(subscription, false, stoppingToken))
                LogResubscribeAllFailed(subscription.TopicUri);
        }

        LogResubscribeAllCompleted();
    }

    public async Task RetryPendingSubscriptionsAsync(CancellationToken stoppingToken = default)
    {
        var query = db.Subscriptions.Where(s => s.Enabled && s.Expiration == null);

        await foreach (var subscription in query.AsAsyncEnumerable().WithCancellation(stoppingToken))
        {
            LogSubscribingPending(subscription.TopicUri);

            if (!await TryNewSubscribeAsync(subscription, false, stoppingToken))
                LogSubscribePendingFailed(subscription.TopicUri);
        }
    }

    public async Task RemoveBrokenSubscriptionsAsync(CancellationToken stoppingToken = default)
    {
        // First, attempt to unsubscribe all orphaned subscriptions via the hub
        await UnsubscribeOrphanedSubscriptionsAsync(stoppingToken);

        // Then clean up broken subscriptions (those without callback URIs)
        var query = db.Subscriptions.Where(s => s.Enabled && s.CallbackUri == null);
        var count = await query.ExecuteDeleteAsync(stoppingToken);

        LogBrokenSubscriptionsRemoved(count);
    }

    public async Task ArchiveSubscriptionsAsync(CancellationToken stoppingToken = default)
    {
        // Copy all current subscriptions with callbacks to the orphaned table
        var orphaned = await db.Subscriptions
            .Where(s => s.CallbackUri != null && s.Slug != null)
            .Select(s => new OrphanedSubscription
            {
                CallbackUri = s.CallbackUri!,
                Slug = s.Slug,
                TopicUri = s.TopicUri,
                Secret = s.Secret,
                Expiration = s.Expiration
            }).ToListAsync(stoppingToken);

        if (orphaned.Count == 0)
        {
            LogSubscriptionsArchived(0);
            return;
        }

        await db.OrphanedSubscriptions.AddRangeAsync(orphaned, stoppingToken);

        await db.SaveChangesAsync(stoppingToken);

        LogSubscriptionsArchived(orphaned.Count);
    }

    public async Task ResetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        await db.Subscriptions.ExecuteUpdateAsync(e => e
            .SetProperty(s => s.CallbackUri, (_) => null)
            .SetProperty(s => s.Expiration, (_) => null)
            .SetProperty(s => s.Mode, (_) => HubMode.Subscribe)
            .SetProperty(s => s.Secret, (_) => null)
            .SetProperty(s => s.Slug, (_) => null)
        , cancellationToken);
    }

    private async Task UnsubscribeOrphanedSubscriptionsAsync(CancellationToken stoppingToken = default)
    {
        var orphans = await db.OrphanedSubscriptions.ToListAsync(stoppingToken);

        foreach (var orphan in orphans)
        {
            LogUnsubscribingOrphan(orphan.CallbackUri);

            var request = new SubscriptionRequest
            {
                CallbackUri = orphan.CallbackUri,
                Mode = HubMode.Unsubscribe,
                TopicUri = orphan.TopicUri,
                Secret = null // Unsubscribe doesn't need secret
            };

            var (success, _) = await ws.TryWebSubAsync(HubUri, request, true, stoppingToken);

            if (!success)
            {
                LogOrphanUnsubscribeFailed(orphan.CallbackUri);
            }
        }
    }
}
