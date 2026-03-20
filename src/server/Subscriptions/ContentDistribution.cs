using System.ComponentModel.DataAnnotations;

namespace NMAC.Subscriptions;

public class ContentDistribution
{
    [Key]
    public required Uri TopicUri { get; set; }

    public string? Content { get; set; }

    public string? Headers { get; set; }

    public string? Metadata { get; set; }

    public DateTimeOffset LastReceivedAt { get; set; }
}