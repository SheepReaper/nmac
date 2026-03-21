using Microsoft.EntityFrameworkCore;

using NMAC.Core;

namespace NMAC.LiveStreams;

public partial class RemoveChannelHandle(
    AppDbContext db,
    TimeProvider tp,
    ILogger<RemoveChannelHandle> logger
) : IUseCase
{
    [LoggerMessage(6202, LogLevel.Information, "Manually removing channel live-detection handle: {Handle}")]
    private partial void LogRemovingHandle(string handle);

    private async Task<IResult> HandleAsync(string handle, CancellationToken ct)
    {
        var normalized = ChannelLiveDetectionWorker.NormalizeHandle(handle);
        LogRemovingHandle(normalized);

        var existing = await db.ChannelLivePollTargets
            .SingleOrDefaultAsync(t => t.Handle == normalized, ct);

        if (existing is null)
            return Results.NotFound();

        existing.Enabled = false;
        existing.UpdatedAt = tp.GetUtcNow();

        await db.SaveChangesAsync(ct);
        return Results.Accepted();
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("remove-channel-handle/{handle}", async (
                string handle,
                RemoveChannelHandle useCase,
                CancellationToken ct) => await useCase.HandleAsync(handle, ct))
                .RequireAuthorization(DeveloperBasicAuthOptions.DeveloperEndpointsPolicy);
        }
    }
}
