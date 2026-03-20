using Microsoft.EntityFrameworkCore;

using NMAC.Core;

namespace NMAC.LiveStreams;

public partial class AddChannelHandle(
    AppDbContext db,
    TimeProvider tp,
    ILogger<AddChannelHandle> logger
) : IUseCase
{
    [LoggerMessage(6201, LogLevel.Information, "Manually adding channel live-detection handle: {Handle}")]
    partial void LogAddingHandle(string handle);

    private async Task<IResult> HandleAsync(string handle, CancellationToken ct)
    {
        var normalized = ChannelLiveDetectionWorker.NormalizeHandle(handle);
        LogAddingHandle(normalized);

        var existing = await db.ChannelLivePollTargets
            .SingleOrDefaultAsync(t => t.Handle == normalized, ct);

        var now = tp.GetUtcNow();

        if (existing is null)
        {
            db.ChannelLivePollTargets.Add(new ChannelLivePollTarget
            {
                Handle = normalized,
                Enabled = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.Enabled = true;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return Results.Accepted();
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("add-channel-handle/{handle}", async (
                string handle,
                AddChannelHandle useCase,
                CancellationToken ct) => await useCase.HandleAsync(handle, ct))
                .RequireAuthorization("DeveloperEndpointsBasicAuth");
        }
    }
}
