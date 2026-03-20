namespace NMAC.Subscriptions;

public class SubscriptionServiceOptions
{
    public required Uri HubUri { get; set; }
    public required Uri CallbackBaseUri { get; set; }
}
