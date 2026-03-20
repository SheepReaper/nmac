// Unusable topic hub: https://www.youtube.com/xml/feeds/videos.xml
// YT hub: http://pubsubhubbub.appspot.com
// Note: Unfortunately YT doesn't implement or has disabled the discoverability part of WebSub, namely GETting the
// the topic URL does not return the hub. However, if you know the hub and the template for the topic URL, you can
// still subscribe to the topic. We will implement the subscription part of the protocol.

using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using NMAC.Core;
using NMAC.Events;

using Wolverine;

namespace NMAC.Subscriptions;

public partial class ReceiveContentDistribution(
    AppDbContext db,
    FeedValidator feedValidator,
    IMessageBus bus,
    TimeProvider tp,
    ILogger<ReceiveContentDistribution> logger
) : IUseCase
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Received content distribution at {PathSlug}.")]
    private partial void LogContentDistributionReceived(Guid PathSlug);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning, Message = "Content distribution request for subscription {Slug} not found.")]
    private partial void LogSubscriptionNotFound(Guid slug);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Failed feed validation for subscription {TopicUri}.")]
    private partial void LogFailedFeedValidation(Uri topicUri);

    private static string SerializeHeaders(IHeaderDictionary headers)
    {
        var normalized = headers.ToDictionary(
            h => h.Key,
            h => string.Join(",", h.Value.ToArray()),
            StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(normalized);
    }

    private static string SerializeMetadata(HttpRequest request, DateTimeOffset receivedAtUtc)
    {
        var metadata = new
        {
            request.Method,
            Path = request.Path.ToString(),
            QueryString = request.QueryString.ToString(),
            request.ContentType,
            request.ContentLength,
            request.HttpContext.TraceIdentifier,
            RemoteIpAddress = request.HttpContext.Connection.RemoteIpAddress?.ToString(),
            ReceivedAtUtc = receivedAtUtc
        };

        return JsonSerializer.Serialize(metadata);
    }

    private async Task<IResult> HandleAsync(Guid slug, HttpRequest request, CancellationToken ct)
    {
        LogContentDistributionReceived(slug);

        if (!(await db.Subscriptions.SingleOrDefaultAsync(s => s.Slug == slug, ct) is { } sub))
        {
            LogSubscriptionNotFound(slug);

            // Tells hub to cancel the subscription
            return Results.StatusCode(StatusCodes.Status410Gone);
        }

        Activity.Current?.AddBaggage("topicUri", sub.TopicUri.ToString());

        var result = string.IsNullOrWhiteSpace(sub.Secret) 
            ? await feedValidator.TryValidateContentDistribution(request)
            : await feedValidator.TryValidateAuthenticatedContentDistribution(request, sub.Secret);

        if (!result.IsValid)
        {
            LogFailedFeedValidation(sub.TopicUri);
            return Results.NotFound();
        }

        var receivedAtUtc = tp.GetUtcNow();

        await db.ExecuteUpsertAsync(
            new ContentDistribution
            {
                TopicUri = sub.TopicUri,
                Content = Encoding.UTF8.GetString(result.BodyBytes),
                Headers = SerializeHeaders(request.Headers),
                Metadata = SerializeMetadata(request, receivedAtUtc),
                LastReceivedAt = receivedAtUtc
            },
            ct
        );

        await bus.PublishAsync(new IngestYTVideoFeed(result.BodyBytes, sub.TopicUri, receivedAtUtc));

        return Results.Ok();
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder endpoints)
        {
            // Content Distribution Endpoint
            endpoints.MapPost("webhooks/youtube/videos/{slug}", async (
                Guid slug,
                HttpRequest request,
                ReceiveContentDistribution useCase,
                CancellationToken ct) => await useCase.HandleAsync(slug, request, ct));
        }
    }
}