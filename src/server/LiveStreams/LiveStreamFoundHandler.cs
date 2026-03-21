using Microsoft.EntityFrameworkCore;

using NMAC.Core;
using NMAC.Events;

namespace NMAC.LiveStreams;

public partial class LiveStreamFoundHandler(
    AppDbContext db,
    ILiveChatCaptureSignal captureSignal,
    ILiveStreamUpdatePublisher updatePublisher,
    ILogger<LiveStreamFoundHandler> logger
)
{
    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "Live stream found: {VideoId}, live chat id: {LiveChatId}")]
    private partial void LogLiveChatFound(string videoId, string liveChatId);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Information, Message = "Enqueued session for live chat {LiveChatId} (video {VideoId}).")]
    private partial void LogSessionEnqueued(string liveChatId, string videoId);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Information, Message = "Session for live chat {LiveChatId} already exists; no action taken.")]
    private partial void LogSessionAlreadyExists(string liveChatId);

    public async Task Handle(LiveStreamFound command, CancellationToken ct)
    {
        LogLiveChatFound(command.VideoId, command.LiveChatId);

        // Check if a session for this live chat already exists.
        var existingSession = await db.LiveChatCaptureSessions
            .FirstOrDefaultAsync(s => s.LiveChatId == command.LiveChatId, ct);

        if (existingSession != null)
        {
            LogSessionAlreadyExists(command.LiveChatId);
            return;
        }

        // Create a new capture session in "Requested" state. The background worker will claim and process it.
        var session = new LiveChatCaptureSession
        {
            SessionId = Guid.NewGuid(),
            LiveChatId = command.LiveChatId,
            VideoId = command.VideoId,
            State = LiveCaptureSessionState.Requested,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.LiveChatCaptureSessions.Add(session);
        await db.SaveChangesAsync(ct);

        // Notify stream catalog subscribers so newly detected live streams appear without manual refresh.
        updatePublisher.PublishStreamCatalogChanged(command.VideoId);

        // Wake the worker immediately so this session can start without polling delay.
        captureSignal.Notify();

        LogSessionEnqueued(command.LiveChatId, command.VideoId);
    }
}