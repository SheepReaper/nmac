using System.Buffers.Text;
using System.Security.Cryptography;
using NMAC.Subscriptions.WebSub;

namespace NMAC.Subscriptions;

public static class WebSubExtensions
{
    private static string GenerateSecret()
    {
        Span<byte> secretBytes = stackalloc byte[32];

        RandomNumberGenerator.Fill(secretBytes);

        return Base64Url.EncodeToString(secretBytes);
    }

    public static bool IsValid(this SubscriptionRequest request, HubMode mode) =>
        request.IsValid(mode, false) &&
        request.CallbackUri.Segments.LastOrDefault() is string s &&
        Guid.TryParse(s, out _);

    public static SubscriptionRequest Resub(this Subscription subscription, Uri? callbackUri = null, string? secret = null, HubMode? mode = null)
    {
        var newReq = subscription.ResubWithoutSecret(callbackUri, mode);

        newReq.Secret = secret ?? GenerateSecret();

        return newReq;
    }

    public static SubscriptionRequest ResubWithoutSecret(this Subscription subscription, Uri? callbackUri = null, HubMode? mode = null) => new()
    {
        CallbackUri = callbackUri ?? subscription.CallbackUri ?? throw new InvalidOperationException("Existing subscription must have a callback URI."),
        Mode = mode ?? HubMode.Subscribe,
        TopicUri = subscription.TopicUri,
        LeaseSeconds = int.MaxValue,
        Secret = null
    };
}