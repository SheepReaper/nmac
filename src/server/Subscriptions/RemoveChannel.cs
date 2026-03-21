using System.Diagnostics;

using NMAC.Core;
using NMAC.Events;

using Wolverine;

namespace NMAC.Subscriptions;

public partial class RemoveChannel(
    ILogger<RemoveChannel> logger,
    IMessageBus bus
) : IUseCase
{
    [LoggerMessage(1102, LogLevel.Information, "Manually removing channel with ID: {ChannelId}")]
    private partial void LogRemovingChannel(string channelId);

    private async Task<IResult> HandleAsync(string channelId, CancellationToken ct)
    {
        LogRemovingChannel(channelId);

        Activity.Current?.AddBaggage("channelId", channelId);
        Activity.Current?.AddBaggage("triggeredBy", "manual");

        await bus.PublishAsync(new UnsubscribeFromChannel(channelId));

        return Results.Accepted();
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("remove-channel/{channelId}", async (
                string channelId,
                RemoveChannel useCase,
                CancellationToken ct) => await useCase.HandleAsync(channelId, ct))
                .RequireAuthorization(DeveloperBasicAuthOptions.DeveloperEndpointsPolicy);
        }
    }
}
