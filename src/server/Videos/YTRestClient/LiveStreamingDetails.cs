namespace NMAC.Videos.YTRestClient;

public class LiveStreamingDetails
{
    public DateTime ActualStartTime { get; set; }
    public DateTime ScheduledStartTime { get; set; }
    public string ConcurrentViewers { get; set; } = string.Empty;
    public string? ActiveLiveChatId { get; set; }
}
