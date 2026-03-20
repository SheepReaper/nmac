namespace NMAC.Ui.LiveStreams;

public interface ILiveStreamDashboardQueryService
{
    Task<IReadOnlyList<LiveStreamListItem>> GetStreamsAsync(bool includeAllDiscovered = false, CancellationToken ct = default);

    Task<LiveStreamDashboard?> GetStreamDashboardAsync(string videoId, CancellationToken ct = default);
}
