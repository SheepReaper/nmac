using System.ComponentModel.DataAnnotations;

namespace NMAC.LiveStreams;

public class LiveChatCaptureSession
{
    [Key]
    public required Guid SessionId { get; set; }

    [MaxLength(128)]
    public required string LiveChatId { get; set; }

    [MaxLength(32)]
    public required string VideoId { get; set; }

    public required LiveCaptureSessionState State { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    public int RetryCount { get; set; }

    [MaxLength(256)]
    public string? LastError { get; set; }
}
