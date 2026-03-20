// Unusable topic hub: https://www.youtube.com/xml/feeds/videos.xml
// YT hub: http://pubsubhubbub.appspot.com
// Note: Unfortunately YT doesn't implement or has disabled the discoverability part of WebSub, namely GETting the
// the topic URL does not return the hub. However, if you know the hub and the template for the topic URL, you can
// still subscribe to the topic. We will implement the subscription part of the protocol.

using System.Diagnostics;
using System.Net.Mime;

using Microsoft.AspNetCore.Mvc;

using NMAC.Core;
using NMAC.Subscriptions.WebSub;

namespace NMAC.Subscriptions;

public partial class VerifySubscription(
    TimeProvider tp,
    AppDbContext db, 
    ILogger<VerifySubscription> logger) : IUseCase
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Received subscription validation request for {Slug}. Mode: {Mode}, Topic: {Topic}, Reason: {Reason}, Challenge: {Challenge}, LeaseSeconds: {LeaseSeconds}")]
    private partial void LogValidationRequestReceived(Guid slug, string mode, Uri topic, string? reason, string? challenge, int? leaseSeconds);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "No subscription found for slug {Slug}.")]
    private partial void LogSubscriptionNotFound(Guid slug);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Subscription denied for topic {Topic}. Reason: {Reason}")]
    private partial void LogSubscriptionDenied(Uri topic, string? reason);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "Invalid validation request for subscription at slug {Slug}. Expected: Mode: {ExpectedMode}, Topic: {ExpectedTopic}. Received: Mode: {ReceivedMode}, Topic: {ReceivedTopic}")]
    private partial void LogInvalidValidationRequest(Guid slug, string? expectedMode, Uri expectedTopic, HubMode receivedMode, Uri receivedTopic);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Unsubscription successful for topic {Topic}.")]
    private partial void LogUnsubscriptionSuccessful(Uri topic);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Subscription successful for topic {Topic}. Lease seconds: {LeaseSeconds}")]
    private partial void LogSubscriptionSuccessful(Uri topic, int? leaseSeconds);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Information, Message = "Orphaned subscription found for slug {Slug}. Processing verification.")]
    private partial void LogOrphanedSubscriptionFound(Guid slug);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Information, Message = "Orphaned subscription for callback {CallbackUri} successfully unsubscribed and removed.")]
    private partial void LogOrphanedSubscriptionRemoved(Uri callbackUri);
    
    private async Task<IResult> HandleAsync(
        Guid slug,
        string mode,
        Uri topic,
        string? reason,
        string? challenge,
        int? leaseSeconds,
        CancellationToken ct)
    {
        Activity.Current?.AddBaggage("topicUri", topic.ToString());

        LogValidationRequestReceived(slug, mode, topic, reason, challenge, leaseSeconds);

        var subscription = db.Subscriptions.SingleOrDefault(s => s.Slug == slug);

        // If not found in active subscriptions, check orphaned subscriptions
        if (subscription == null)
        {
            var orphaned = db.OrphanedSubscriptions.SingleOrDefault(o => o.Slug == slug);

            if (orphaned != null)
            {
                LogOrphanedSubscriptionFound(slug);

                HubMode orphanMode = mode;

                // Only process unsubscribe verification for orphaned subscriptions
                if (orphanMode == HubMode.Unsubscribe && orphaned.TopicUri == topic)
                {
                    LogOrphanedSubscriptionRemoved(orphaned.CallbackUri);
                    db.OrphanedSubscriptions.Remove(orphaned);
                    await db.SaveChangesAsync(ct);
                    return Results.Text(challenge, MediaTypeNames.Text.Plain);
                }
            }

            LogSubscriptionNotFound(slug);
            return Results.NotFound();
        }

        HubMode hubMode = mode;

        if (hubMode == HubMode.Denied)
        {
            LogSubscriptionDenied(topic, reason);

            subscription.Expiration = null;
            subscription.Mode = null;
            subscription.Secret = null;
            subscription.Slug = null;

            return Results.Ok();
        }

        if (!(subscription.Mode! == hubMode.Value && subscription.TopicUri == topic))
        {
            LogInvalidValidationRequest(slug, subscription.Mode, subscription.TopicUri, hubMode, topic);
            return Results.NotFound();
        }

        if (hubMode == HubMode.Unsubscribe)
        {
            LogUnsubscriptionSuccessful(topic);

            subscription.Expiration = null;
            subscription.Mode = HubMode.Unsubscribe;
            subscription.Secret = null;
            subscription.Slug = null;
        }

        if (leaseSeconds.HasValue && hubMode == HubMode.Subscribe)
        {
            LogSubscriptionSuccessful(topic, leaseSeconds);
            subscription.Expiration = tp.GetUtcNow().AddSeconds(leaseSeconds.Value);
        }

        await db.SaveChangesAsync(ct);

        return Results.Text(challenge, MediaTypeNames.Text.Plain);
    }

    public class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("webhooks/youtube/videos/{slug}", async (
                Guid slug,
                [FromQuery(Name = "hub.mode")] string mode,
                [FromQuery(Name = "hub.topic")] Uri topic,
                [FromQuery(Name = "hub.reason")] string? reason,
                [FromQuery(Name = "hub.challenge")] string? challenge,
                [FromQuery(Name = "hub.lease_seconds")] int? leaseSeconds,
                VerifySubscription useCase,
                CancellationToken ct) => await useCase.HandleAsync(slug, mode, topic, reason, challenge, leaseSeconds, ct));
        }
    }
}