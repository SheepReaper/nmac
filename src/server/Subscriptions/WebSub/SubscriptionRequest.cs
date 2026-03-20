namespace NMAC.Subscriptions.WebSub;

public class SubscriptionRequest
{
    public required Uri CallbackUri { get; set; }
    public required HubMode Mode { get; set; }
    public required Uri TopicUri { get; set; }
    public int? LeaseSeconds { get; set; }
    public string? Secret { get; set; }

    public FormUrlEncodedContent ToFormUrlEncodedContent()
    {
        var dict = new Dictionary<string, string>
        {
            ["hub.callback"] = CallbackUri.ToString(),
            ["hub.mode"] = Mode.ToString(),
            ["hub.topic"] = TopicUri.ToString()
        };

        if (LeaseSeconds.HasValue)
            dict["hub.lease_seconds"] = LeaseSeconds.Value.ToString();

        if (!string.IsNullOrEmpty(Secret))
            dict["hub.secret"] = Secret;

        return new FormUrlEncodedContent(dict);
    }

    public static implicit operator FormUrlEncodedContent(SubscriptionRequest request) => request.ToFormUrlEncodedContent();

    public bool IsValidSubscription(bool insecure = false) => Mode == HubMode.Subscribe && (!string.IsNullOrWhiteSpace(Secret) || insecure);

    public bool IsValidUnsubscription(bool insecure = false) => Mode == HubMode.Unsubscribe && (!string.IsNullOrWhiteSpace(Secret) || insecure);

    public bool IsValid(HubMode mode, bool insecure) => mode switch
    {
        { } when mode == HubMode.Subscribe => IsValidSubscription(insecure),
        { } when mode == HubMode.Unsubscribe => IsValidUnsubscription(insecure),
        _ => false
    };

    public Guid ExtractGuidSlug() => (CallbackUri.Segments.LastOrDefault() is string s && Guid.TryParse(s, out var parsedGuid))
        ? parsedGuid
        : throw new InvalidOperationException("Invalid callback URI format. Expected last segment to be a GUID slug.");
}
