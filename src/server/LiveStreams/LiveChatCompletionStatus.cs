namespace NMAC.LiveStreams;

public record LiveChatCompletionStatus
{
    private const string CONTINUE = "continue";
    private const string CHAT_ENDED = "chat-ended";
    private const string OFFLINE = "offline";
    private const string CANCELLED = "cancelled";
    private const string FAILED = "failed";

    public string Value { get; private set; }

    public static readonly LiveChatCompletionStatus Continue = new(CONTINUE);
    public static readonly LiveChatCompletionStatus ChatEnded = new(CHAT_ENDED);
    public static readonly LiveChatCompletionStatus Offline = new(OFFLINE);
    public static readonly LiveChatCompletionStatus Cancelled = new(CANCELLED);
    public static readonly LiveChatCompletionStatus Failed = new(FAILED);

    private LiveChatCompletionStatus(string value)
    {
        Value = value;
    }

    public override string ToString() => Value;

    public static implicit operator string(LiveChatCompletionStatus status) => status.Value;

    public static LiveChatCompletionStatus FromString(string value) => value.ToLowerInvariant() switch
    {
        CONTINUE => Continue,
        CHAT_ENDED => ChatEnded,
        OFFLINE => Offline,
        CANCELLED => Cancelled,
        FAILED => Failed,
        _ => throw new ArgumentException($"Invalid live chat completion status: {value}")
    };

    public static implicit operator LiveChatCompletionStatus(string value) => FromString(value);
}
