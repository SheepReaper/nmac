using System.ComponentModel.DataAnnotations;

namespace NMAC.LiveStreams;

public class LiveFundingDonation
{
  [Key]
  [MaxLength(128)]
  public required string MessageId { get; set; }

  [MaxLength(32)]
  public required string VideoId { get; set; }

  [MaxLength(128)]
  public required string LiveChatId { get; set; }

  /// <summary>Null when the donor is anonymous.</summary>
  [MaxLength(64)]
  public string? AuthorChannelId { get; set; }

  [MaxLength(200)]
  public string? AuthorDisplayName { get; set; }

  public long AmountMicros { get; set; }

  [MaxLength(32)]
  public string? Currency { get; set; }

  [MaxLength(64)]
  public string? AmountDisplayString { get; set; }

  /// <summary>Optional comment the donor attached to the donation.</summary>
  public string? UserComment { get; set; }

  public DateTimeOffset? PublishedAt { get; set; }
}
