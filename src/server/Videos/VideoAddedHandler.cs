using System.Diagnostics;

using NMAC.Core;
using NMAC.Events;
using NMAC.Videos.YTRestClient;

namespace NMAC.Videos;

public partial class VideoAddedHandler(
    IYouTubeLiveChatApi api,
    AppDbContext db,
    ILogger<VideoAddedHandler> logger)
{
    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Video manually added: {VideoId}")]
    partial void LogNewVideo(string videoId);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "Live chat found for video: {VideoId}, LiveChatId: {LiveChatId}")]
    partial void LogLiveChatFound(string videoId, string liveChatId);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Warning, Message = "YouTube API error for video {VideoId}: {ErrorMessage}")]
    partial void LogYTApiError(string videoId, string? errorMessage);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning, Message = "Video {VideoId} not found in YouTube API response.")]
    partial void LogRemoteVideoNotFound(string videoId);

    private readonly static string[] ThumbnailPref = ["maxres", "high"];

    public async Task<LiveStreamFound?> Handle(VideoAdded command, CancellationToken ct)
    {
        // var parentContext = command.TraceParent is null ? default : ActivityContext.Parse(command.TraceParent, null);

        // using var activity = Telemetry.ActivitySource.StartActivity("Consume VideoAdded", ActivityKind.Consumer, parentContext);
        Activity.Current?.SetTag("video.id", command.VideoId);
        Activity.Current?.AddBaggage("videoId", command.VideoId);

        LogNewVideo(command.VideoId);

        var result = await api.GetVideosAsync("snippet,liveStreamingDetails", command.VideoId, ct);

        if (!result.IsSuccessful)
        {
            LogYTApiError(command.VideoId, result.Error?.Message);
            return null;
        }

        if (result.Content.Items.FirstOrDefault() is not { } video)
        {
            LogRemoteVideoNotFound(command.VideoId);
            return null;
        }

        YTVideo newVideo = new()
        {
            VideoId = video.Id,
            ChannelId = video.Snippet?.ChannelId,
            TopicUri = new Uri($"http://www.youtube.com/feeds/videos.xml?channel_id={video.Snippet?.ChannelId}"),
            Title = video.Snippet?.Title,
            PublishedAt = video.Snippet?.PublishedAt ?? DateTimeOffset.MinValue,
            UpdatedAt = video.Snippet?.PublishedAt ?? DateTimeOffset.MinValue,
            WatchUrl = new Uri($"https://www.youtube.com/watch?v={video.Id}"),
            Description = video.Snippet?.Description
        };

        foreach (var pref in ThumbnailPref)
        {
            if (video.Snippet?.Thumbnails?.TryGetValue(pref, out var tn) ?? false)
            {
                newVideo.ThumbnailUrl = tn.Url;
                break;
            }
        }

        await db.ExecuteUpsertAsync(newVideo, ct);

        if (video.LiveStreamingDetails?.ActiveLiveChatId is not string liveChatId)
            return null;

        LogLiveChatFound(command.VideoId, liveChatId);

        // using var publishActivity = Telemetry.ActivitySource.StartActivity("Publish LiveStreamFound", ActivityKind.Producer);
        // publishActivity?.SetTag("video.id", command.VideoId);
        Activity.Current?.SetTag("live_chat.id", liveChatId);
        Activity.Current?.AddBaggage("liveChatId", liveChatId);

        return new LiveStreamFound(command.VideoId, liveChatId);
    }
}