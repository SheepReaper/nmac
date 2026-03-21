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
    [LoggerMessage(5005, LogLevel.Information, "Manually adding video with ID: {VideoId}")]
    private partial void LogAddingVideo(string videoId);

    private async Task<IResult> HandleAsync(string videoId, CancellationToken ct)
    {
        Activity.Current?.AddBaggage("videoId", videoId);
        Activity.Current?.AddBaggage("triggeredBy", "manual");

        LogAddingVideo(videoId);

        await bus.PublishAsync(new VideoAdded(videoId));

        return Results.Accepted();
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("add-video/{videoId}", async (
                string videoId,
                AddVideo useCase,
                CancellationToken ct) => await useCase.HandleAsync(videoId, ct))
                .RequireAuthorization(DeveloperBasicAuthOptions.DeveloperEndpointsPolicy);
        }
    }
}