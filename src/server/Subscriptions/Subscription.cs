using System.ComponentModel.DataAnnotations;

namespace NMAC.Subscriptions;

public class Subscription
{
    public Guid? Slug { get; set; }

    [Key]
    public required Uri TopicUri { get; set; }

    [MaxLength(64)]
    public string? Secret { get; set; }

    [MaxLength(11)]
    public string? Mode { get; set; }

    public DateTimeOffset? Expiration { get; set; }

    public Uri? CallbackUri { get; set; }

    public bool Enabled { get; set; }
}
