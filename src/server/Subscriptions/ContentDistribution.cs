using System.ComponentModel.DataAnnotations;

namespace NMAC.Subscriptions;

public class ContentDistribution
{
    [Key]
    public required Uri TopicUri { get; set; }

    // Raw webhook body is stored as unbounded text by design.
    public string? Content { get; set; }

    // Serialized inbound headers are intentionally unbounded text.
    public string? Headers { get; set; }

    // Metadata blob is intentionally unbounded text.
    public string? Metadata { get; set; }

    public DateTimeOffset LastReceivedAt { get; set; }
}