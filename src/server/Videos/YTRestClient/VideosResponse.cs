using System.Collections.ObjectModel;

namespace NMAC.Videos.YTRestClient;

public class VideosResponse
{
    public string Kind { get; set; } = string.Empty;
    public string Etag { get; set; } = string.Empty;
    public Collection<VideoResource> Items { get; set; } = [];
    public PageInfo PageInfo { get; set; } = new();
    public string? PrevPageToken { get; set; }
    public string? NextPageToken { get; set; }
}
