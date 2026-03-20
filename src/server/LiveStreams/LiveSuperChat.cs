using System.ComponentModel.DataAnnotations;

namespace NMAC.LiveStreams;

public class LiveSuperChat
{
    [Key]
    [MaxLength(128)]
    public required string MessageId { get; set; }

    [MaxLength(32)]
    public required string VideoId { get; set; }

    [MaxLength(128)]
    public required string LiveChatId { get; set; }

    [MaxLength(64)]
    public string? AuthorChannelId { get; set; }

    [MaxLength(200)]
    public string? AuthorDisplayName { get; set; }

    public string? AuthorProfileImageUrl { get; set; }

    public long AmountMicros { get; set; }

    [MaxLength(32)]
    public string? Currency { get; set; }

    [MaxLength(64)]
    public string? AmountDisplayString { get; set; }

    public string? MessageContent { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
}
