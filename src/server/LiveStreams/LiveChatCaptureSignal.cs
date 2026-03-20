using System.Threading.Channels;

namespace NMAC.LiveStreams;

public interface ILiveChatCaptureSignal
{
    void Notify();

    ValueTask WaitAsync(CancellationToken ct);
}

public sealed class LiveChatCaptureSignal : ILiveChatCaptureSignal
{
    // Capacity 1 coalesces bursty notifications into a single wake-up signal.
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public void Notify()
    {
        _channel.Writer.TryWrite(true);
    }

    public async ValueTask WaitAsync(CancellationToken ct)
    {
        _ = await _channel.Reader.ReadAsync(ct);
    }
}