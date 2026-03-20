using System.Collections.Concurrent;

using NMAC.Ui.LiveStreams;

namespace NMAC.LiveStreams;

public interface ILiveStreamUpdatePublisher
{
    void PublishSuperChatsPersisted(string videoId, int newSuperChatsPersisted);
    void PublishStreamCatalogChanged(string videoId);
}

public sealed class LiveStreamUpdateNotifier(ILogger<LiveStreamUpdateNotifier> logger)
    : ILiveStreamUpdateNotifier, ILiveStreamUpdatePublisher
{
    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new();
    private const string AllVideosSubscriptionKey = "*";

    public IDisposable Subscribe(string videoId, Action<LiveStreamUpdate> onUpdate)
    {
        var id = Guid.NewGuid();
        var subscription = new Subscription(id, videoId, onUpdate);
        _subscriptions[id] = subscription;

        return new Unsubscriber(_subscriptions, id);
    }

    public IDisposable SubscribeAll(Action<LiveStreamUpdate> onUpdate)
        => Subscribe(AllVideosSubscriptionKey, onUpdate);

    public void PublishSuperChatsPersisted(string videoId, int newSuperChatsPersisted)
    {
        if (newSuperChatsPersisted <= 0)
            return;

        var update = new LiveStreamUpdate(videoId, DateTimeOffset.UtcNow, newSuperChatsPersisted);

        NotifySubscribers(videoId, update);
    }

    public void PublishStreamCatalogChanged(string videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId))
            return;

        var update = new LiveStreamUpdate(videoId, DateTimeOffset.UtcNow, 0);

        NotifySubscribers(videoId, update);
    }

    private void NotifySubscribers(string videoId, LiveStreamUpdate update)
    {
        var subscribers = _subscriptions.Values.Where(s =>
            string.Equals(s.VideoId, videoId, StringComparison.Ordinal)
            || string.Equals(s.VideoId, AllVideosSubscriptionKey, StringComparison.Ordinal));

        foreach (var sub in subscribers)
        {
            try
            {
                sub.OnUpdate(update);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed notifying stream subscriber for video {VideoId}", videoId);
            }
        }
    }

    private sealed record Subscription(Guid Id, string VideoId, Action<LiveStreamUpdate> OnUpdate);

    private sealed class Unsubscriber(ConcurrentDictionary<Guid, Subscription> subscriptions, Guid id) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            subscriptions.TryRemove(id, out _);
        }
    }
}
