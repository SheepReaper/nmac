namespace NMAC.Videos.YTRestClient;

public class VideoResource
{
    public string Kind { get; set; } = string.Empty;
    public string Etag { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public VideoSnippet? Snippet { get; set; }
    public LiveStreamingDetails? LiveStreamingDetails { get; set; }
}

public class VideoSnippet
{
    public DateTimeOffset PublishedAt { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, Thumbnail> Thumbnails { get; set; } = [];
    public string ChannelTitle { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string CategoryId { get; set; } = string.Empty;
    public string LiveBroadcastContent { get; set; } = string.Empty;
    public string DefaultLanguage { get; set; } = string.Empty;
    public Localized? Localized { get; set; }
    public string DefaultAudioLanguage { get; set; } = string.Empty;

}

public class Thumbnail
{
    public required Uri Url { get; set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
}

public class Localized
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}