namespace NMAC.Ui.LiveStreams;

public interface ILiveStreamUpdateNotifier
{
    IDisposable Subscribe(string videoId, Action<LiveStreamUpdate> onUpdate);
    IDisposable SubscribeAll(Action<LiveStreamUpdate> onUpdate);
}
