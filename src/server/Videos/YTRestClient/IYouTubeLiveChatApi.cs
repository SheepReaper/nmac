using Refit;

namespace NMAC.Videos.YTRestClient;

public interface IYouTubeLiveChatApi
{
    // Get live chat messages
    [Get("/liveChat/messages")]
    Task<ApiResponse<LiveChatMessagesResponse>> GetLiveChatMessagesAsync(
        string liveChatId,
        string part,
        string apiKey,
        string? pageToken = null,
        CancellationToken cancellationToken = default
    );

    // Get Video
    [Get("/videos")]
    Task<ApiResponse<VideosResponse>> GetVideosAsync(
        string part,
        string? id = null,
        CancellationToken cancellationToken = default
    );
}