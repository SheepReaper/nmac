using System.Diagnostics;

using NMAC.Core;
using NMAC.Events;

using Wolverine;

namespace NMAC.Videos;

public partial class AddVideo(
    IMessageBus bus,
    ILogger<AddVideo> logger
    ) : IUseCase
{
    [LoggerMessage(0, LogLevel.Information, "Manually adding video with ID: {VideoId}")]
    private partial void LogAddingVideo(string videoId);

    private async Task HandleAsync(string videoId)
    {
        Activity.Current?.AddBaggage("videoId", videoId);
        Activity.Current?.AddBaggage("triggeredBy", "manual");

        LogAddingVideo(videoId);

        await bus.PublishAsync(new VideoAdded(videoId));
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("add-video/{videoId}", async (
                string videoId, 
                AddVideo useCase) => await useCase.HandleAsync(videoId))
                .RequireAuthorization("DeveloperEndpointsBasicAuth");
        }
    }
}