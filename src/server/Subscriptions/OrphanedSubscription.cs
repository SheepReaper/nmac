using System.ComponentModel.DataAnnotations;

namespace NMAC.Subscriptions;

public class OrphanedSubscription
{
    [Key]
    public required Uri CallbackUri { get; set; }

    public Guid? Slug { get; set; }

    public required Uri TopicUri { get; set; }

    [MaxLength(64)]
    public string? Secret { get; set; }

    public DateTimeOffset? Expiration { get; set; }
}
