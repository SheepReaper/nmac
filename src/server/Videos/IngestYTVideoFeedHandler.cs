using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

using Microsoft.EntityFrameworkCore;

using NMAC.Core;
using NMAC.Events;
using NMAC.Subscriptions.WebSub.Atom;

using Wolverine;

namespace NMAC.Videos;

public partial class IngestYTVideoFeedHandler(
    AppDbContext db,
    IMessageBus bus,
    XmlSerializer xmlSerializer,
    ILogger<IngestYTVideoFeedHandler> logger)
{
    [LoggerMessage(EventId = 2201, Level = LogLevel.Warning, Message = "Failed to parse Atom feed for subscription {TopicUri}. Message: {Message}")]
    private partial void LogFeedParseFailed(Uri topicUri, string message);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Information, Message = "Processed {NewCount} new and {UpdatedCount} updated YTVideo snapshots for subscription {TopicUri}.")]
    private partial void LogYTVideosProcessed(int newCount, int updatedCount, Uri topicUri);

    [LoggerMessage(EventId = 2203, Level = LogLevel.Error, Message = "Failed to deserialize Atom feed for subscription {TopicUri}.")]
    private partial void LogDeserializationFailed(Uri topicUri);

    [LoggerMessage(EventId = 2204, Level = LogLevel.Warning, Message = "Malformed link tags detected for subscription {TopicUri}. Retrying parse after repair.")]
    private partial void LogRetryingAfterLinkRepair(Uri topicUri);

    [LoggerMessage(EventId = 2205, Level = LogLevel.Warning, Message = "Error during Atom feed deserialization. Message: {Message}")]
    private partial void LogDeserializationError(string? message);

    private static readonly Regex UnclosedLinkRegex = MyRegex();

    private bool TryDeserializeFeed(byte[] body, [NotNullWhen(true)] out Feed? feed)
    {
        using var reader = XmlReader.Create(new MemoryStream(body, writable: false), new() { DtdProcessing = DtdProcessing.Prohibit });

        try
        {
            feed = xmlSerializer.Deserialize(reader) as Feed;
        }
        catch (InvalidOperationException ex)
        {
            feed = null;
            LogDeserializationError(ex.InnerException?.Message);
        }

        return feed is not null;
    }

    private static byte[] RepairMalformedLinkTags(byte[] body)
    {
        var xml = Encoding.UTF8.GetString(body);

        var repaired = UnclosedLinkRegex.Replace(xml, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            if (attrs.TrimEnd().EndsWith('/'))
            {
                return match.Value;
            }

            return $"<link{attrs} />";
        });

        return Encoding.UTF8.GetBytes(repaired);
    }

    private List<YTVideo> ParseYTVideos(byte[] body, Uri topicUri, DateTimeOffset receivedAtUtc)
    {
        if (!TryDeserializeFeed(body, out var feed))
        {
            LogRetryingAfterLinkRepair(topicUri);

            var repairedBody = RepairMalformedLinkTags(body);
            if (!TryDeserializeFeed(repairedBody, out feed))
            {
                LogDeserializationFailed(topicUri);
                return [];
            }
        }

        if (feed is null)
        {
            return [];
        }

        var deduped = new Dictionary<string, YTVideo>(StringComparer.Ordinal);

        foreach (var entry in feed.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.VideoId)) continue;

            var candidate = new YTVideo
            {
                VideoId = entry.VideoId,
                ChannelId = entry.ChannelId,
                TopicUri = topicUri,
                Title = entry.Title,
                PublishedAt = entry.Published,
                UpdatedAt = entry.Updated,
                WatchUrl = entry.WatchUrl,
                Description = entry.MediaGroup?.Description,
                ThumbnailUrl = entry.MediaGroup?.Thumbnail?.Url,
                LastSeenAt = receivedAtUtc
            };

            if (!deduped.TryGetValue(entry.VideoId, out var existing))
            {
                deduped[entry.VideoId] = candidate;
                continue;
            }

            var existingVersion = existing.UpdatedAt ?? existing.PublishedAt ?? DateTimeOffset.MinValue;
            var candidateVersion = candidate.UpdatedAt ?? candidate.PublishedAt ?? DateTimeOffset.MinValue;

            if (candidateVersion > existingVersion)
            {
                deduped[entry.VideoId] = candidate;
            }
        }

        return [.. deduped.Values];
    }

    public async Task HandleAsync(IngestYTVideoFeed command, CancellationToken ct)
    {
        try
        {
            var videos = ParseYTVideos(command.FeedBody, command.TopicUri, command.ReceivedAtUtc);

            var videoIds = videos.Select(v => v.VideoId).ToArray();
            var existingVideoIds = await db.YTVideos
                .AsNoTracking()
                .Where(v => videoIds.Contains(v.VideoId))
                .Select(v => v.VideoId)
                .ToListAsync(ct);

            var existingSet = existingVideoIds.ToHashSet(StringComparer.Ordinal);
            var newVideos = new List<YTVideo>(videos.Count);
            var existingVideos = new List<YTVideo>(videos.Count);

            foreach (var video in videos)
            {
                if (existingSet.Contains(video.VideoId))
                {
                    existingVideos.Add(video);
                }
                else
                {
                    newVideos.Add(video);
                }
            }

            // This is the pre-upsert new figure you wanted from the split operation.
            var newCount = newVideos.Count;
            var updatedCount = 0;

            foreach (var video in newVideos)
            {
                var outcome = await db.ExecuteUpsertAsync(video, ct);

                if (outcome == YTVideoUpsertOutcome.Inserted)
                {
                    await bus.PublishAsync(new VideoDiscovered(video.VideoId));
                    continue;
                }

                if (outcome == YTVideoUpsertOutcome.Updated)
                {
                    await bus.PublishAsync(new VideoUpdated(video.VideoId));
                    updatedCount++;
                }
            }

            foreach (var video in existingVideos)
            {
                var outcome = await db.ExecuteUpsertAsync(video, ct);
                if (outcome == YTVideoUpsertOutcome.Updated)
                {
                    await bus.PublishAsync(new VideoUpdated(video.VideoId));
                    updatedCount++;
                }
                else if (outcome == YTVideoUpsertOutcome.Inserted)
                {
                    // Handles race conditions between pre-query and write.
                    await bus.PublishAsync(new VideoDiscovered(video.VideoId));
                }
            }

            LogYTVideosProcessed(newCount, updatedCount, command.TopicUri);
        }
        catch (Exception ex)
        {
            LogFeedParseFailed(command.TopicUri, ex.Message);
        }
    }

    [GeneratedRegex("<link(?<attrs>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
}
