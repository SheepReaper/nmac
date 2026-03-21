using System.ComponentModel.DataAnnotations;

namespace NMAC.LiveStreams;

public class AvatarCacheItem
{
    [Key]
    [MaxLength(64)]
    public required string CacheKey { get; set; }

    public required string SourceUrl { get; set; }

    [MaxLength(128)]
    public string? ContentType { get; set; }

    public byte[]? Content { get; set; }

    public bool IsMissing { get; set; }

    public DateTimeOffset CachedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}