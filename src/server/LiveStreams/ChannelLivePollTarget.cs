using System.ComponentModel.DataAnnotations;

namespace NMAC.LiveStreams;

public class ChannelLivePollTarget
{
    [Key]
    [MaxLength(128)]
    public required string Handle { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
