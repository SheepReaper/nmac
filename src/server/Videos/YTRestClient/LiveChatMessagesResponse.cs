using System.Collections.ObjectModel;

namespace NMAC.Videos.YTRestClient;

public class LiveChatMessagesResponse
{
    public string? NextPageToken { get; set; }
    public string? PollingIntervalMillis { get; set; }
    public Collection<LiveChatMessage>? Items { get; set; }
}
