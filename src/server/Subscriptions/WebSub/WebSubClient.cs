using System.Net;

namespace NMAC.Subscriptions.WebSub;

public partial class WebSubClient(HttpClient http, ILogger<WebSubClient> logger)
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Error, Message = "Invalid subscription request for topic {TopicUri} with mode {Mode}. Secret is {SecretStatus}. Insecure flag is {Insecure}.")]
    partial void LogInvalidSubscriptionRequest(Uri topicUri, HubMode mode, string secretStatus, bool insecure);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information, Message = "Requesting '{Mode}' to topic {TopicUri} at {HubUri} with callback uri: {CallbackUri}, lease seconds: {LeaseSeconds}, hmac secret: {HMACSecret}.")]
    partial void LogRequestingSubscription(HubMode mode, Uri topicUri, Uri hubUri, Uri callbackUri, int? leaseSeconds, string? hmacSecret);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Error, Message = "Failed to (re)subscribe to topic {TopicUri}. Status code: {StatusCode}. Reason phrase: {ReasonPhrase}. Message body: {MessageBody}")]
    partial void LogFailedSubscription(Uri topicUri, HttpStatusCode statusCode, string? reasonPhrase, string messageBody);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Information, Message = "Successfully requested subscription to topic {TopicUri}.")]
    partial void LogSuccessfulSubscription(Uri topicUri);

    [LoggerMessage(EventId = 4005, Level = LogLevel.Warning, Message = "Unexpected status code {StatusCode} when subscribing to topic {TopicUri}. Expected 202 Accepted.")]
    partial void LogUnexpectedStatusCode(HttpStatusCode statusCode, Uri topicUri);

    public async Task<(bool success, Uri? migratedTopicUri)> TryWebSubAsync(
        Uri hubUri,
        SubscriptionRequest request,
        bool insecure = false,
        CancellationToken stoppingToken = default
    )
    {
        if (!request.IsValid(request.Mode, insecure))
        {
            LogInvalidSubscriptionRequest(request.TopicUri, request.Mode, string.IsNullOrWhiteSpace(request.Secret) ? "null or whitespace" : "set", insecure);

            return (false, null);
        }

        // Normally you would handle subscription migrations
        // 1. Fetch the topic Uri (client will follow redirects)
        // 2. Parse response for new self link
        // 3. verify new topic is valid
        // 4. Return new Uri in tuple
        // var (valid, alternates, hub) = await WebSub.ValidateFeedAsync(request.TopicUri.ToString(), http, stoppingToken);
        // However, fetching youtube's topics resuls in 404. So we're going to assume the topic is valid and ignore migrations for now.

        LogRequestingSubscription(request.Mode, request.TopicUri, hubUri, request.CallbackUri, request.LeaseSeconds, request.Secret);

        using var response = await http.PostAsync(hubUri, request, stoppingToken);

        if (!response.IsSuccessStatusCode)
        {
            LogFailedSubscription(request.TopicUri, response.StatusCode, response.ReasonPhrase, await response.Content.ReadAsStringAsync(stoppingToken));

            return (false, null);
        }

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            LogSuccessfulSubscription(request.TopicUri);
        }
        else
        {
            LogUnexpectedStatusCode(response.StatusCode, request.TopicUri);
        }

        return (true, null);
    }
}
