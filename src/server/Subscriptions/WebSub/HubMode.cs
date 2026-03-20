namespace NMAC.Subscriptions.WebSub;

public record HubMode
{
    private const string SUBSCRIBE = "subscribe";
    private const string UNSUBSCRIBE = "unsubscribe";
    private const string DENIED = "denied";
    public string Value { get; private set; }
    public static readonly HubMode Subscribe = new(SUBSCRIBE);
    public static readonly HubMode Unsubscribe = new(UNSUBSCRIBE);
    public static readonly HubMode Denied = new(DENIED);
    private HubMode(string value) { Value = value; }

    public override string ToString() => Value;
    public static implicit operator string(HubMode mode) => mode.Value;
    public static HubMode FromString(string value) => value.ToLowerInvariant() switch
    {
        SUBSCRIBE => Subscribe,
        UNSUBSCRIBE => Unsubscribe,
        DENIED => Denied,
        _ => throw new ArgumentException($"Invalid hub mode: {value}")
    };

    public static implicit operator HubMode(string value) => FromString(value);
}