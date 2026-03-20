namespace NMAC.LiveStreams;

public class ChannelLivePollingOptions
{
    public bool Enabled { get; set; } = false;
    public int PollIntervalSeconds { get; set; } = 60;
    public int RecheckIntervalSeconds { get; set; } = 900;
    public string[] SeedHandles { get; set; } = [];
}
