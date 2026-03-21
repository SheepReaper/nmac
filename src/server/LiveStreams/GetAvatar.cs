using Microsoft.AspNetCore.Http;

using NMAC.Core;

namespace NMAC.LiveStreams;

public sealed class GetAvatar
{
    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("avatars/{authorKey}", async (
                string authorKey,
                string? url,
                AvatarProxyService avatarProxy,
                HttpContext httpContext,
                CancellationToken ct) =>
            {
                _ = authorKey;

                if (string.IsNullOrWhiteSpace(url))
                    return Results.NoContent();

                var avatar = await avatarProxy.GetAvatarAsync(url, ct);
                if (!avatar.HasContent)
                    return Results.NoContent();

                httpContext.Response.Headers.CacheControl = "public,max-age=86400";
                return Results.File(avatar.Content, avatar.ContentType);
            });
        }
    }
}