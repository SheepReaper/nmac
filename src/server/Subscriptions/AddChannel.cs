using System.Diagnostics;

using NMAC.Core;
using NMAC.Events;

using Wolverine;

namespace NMAC.Subscriptions;

public partial class AddChannel(
    ILogger<AddChannel> logger,
    IMessageBus bus
) : IUseCase
{
    [LoggerMessage(0, LogLevel.Information, "Manually adding channel with ID: {ChannelId}")]
    private partial void LogAddingChannel(string channelId);
    
    private async Task<IResult> HandleAsync(string channelId)
    {
        LogAddingChannel(channelId);

        Activity.Current?.AddBaggage("channelId", channelId);
        Activity.Current?.AddBaggage("triggeredBy", "manual");

        await bus.PublishAsync(new SubscribeToChannel(channelId));

        return Results.Accepted();
    }
    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("add-channel/{channelId}", async (
                string channelId, 
                AddChannel useCase) => await useCase.HandleAsync(channelId))
                .RequireAuthorization("DeveloperEndpointsBasicAuth");
        }
    }
}