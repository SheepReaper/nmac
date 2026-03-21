using System.ComponentModel.DataAnnotations;

namespace NMAC.Videos;

public class YTVideo
{
    [Key]
    [MaxLength(32)]
    public required string VideoId { get; set; }

    [MaxLength(64)]
    public string? ChannelId { get; set; }

    public required Uri TopicUri { get; set; }

    [MaxLength(500)]
    public string? Title { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Uri? WatchUrl { get; set; }

    // YouTube descriptions are intentionally stored as unbounded text.
    public string? Description { get; set; }

    public Uri? ThumbnailUrl { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}
