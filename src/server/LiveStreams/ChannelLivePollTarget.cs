using System.ComponentModel.DataAnnotations;

namespace NMAC.LiveStreams;

public class ChannelLivePollTarget
{
    [Key]
    [MaxLength(128)]
    public required string Handle { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
