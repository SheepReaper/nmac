using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;

using JasperFx.Core;

namespace NMAC.Subscriptions;

public partial class FeedValidator(
    ILogger<FeedValidator> logger)
{
    [LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Missing Content-Type header in content distribution request.")]
    private partial void LogMissingContentType();

    [LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "Unsupported Content-Type header in content distribution request: {mediaType}")]
    private partial void LogUnsupportedContentType(string? mediaType);

    [LoggerMessage(EventId = 2103, Level = LogLevel.Warning, Message = "Missing Link header in content distribution request.")]
    private partial void LogMissingLinkHeader();

    [LoggerMessage(EventId = 2104, Level = LogLevel.Warning, Message = "Missing hub Link in content distribution request. Link header value: {linkHeaderValue}")]
    private partial void LogMissingHubLink(string linkHeaderValue);

    [LoggerMessage(EventId = 2105, Level = LogLevel.Warning, Message = "Missing self Link in content distribution request. Link header value: {linkHeaderValue}")]
    private partial void LogMissingSelfLink(string linkHeaderValue);

    [LoggerMessage(EventId = 2106, Level = LogLevel.Warning, Message = "Missing X-Hub-Signature header in content distribution request.")]
    private partial void LogMissingSignatureHeader();

    [LoggerMessage(EventId = 2107, Level = LogLevel.Warning, Message = "Invalid X-Hub-Signature header value: {signatureHeaderValue}")]
    private partial void LogSignatureParseError(string signatureHeaderValue);

    [LoggerMessage(EventId = 2108, Level = LogLevel.Warning, Message = "Unsupported hash algorithm in X-Hub-Signature header: {algo}")]
    private partial void LogUnsupportedHashAlgo(string algo);

    [LoggerMessage(EventId = 2109, Level = LogLevel.Warning, Message = "Signature validation failed for content distribution request.")]
    private partial void LogSignatureValidationFailed();

    [LoggerMessage(EventId = 2110, Level = LogLevel.Warning, Message = "Failed header validation for content distribution request.")]
    private partial void LogHeaderValidationFailed();

    private const string AtomFeedTypeName = "application/atom+xml";

    private static readonly string[] SupportedContentTypes = [
        MediaTypeNames.Application.Xml,
        MediaTypeNames.Text.Xml,
        AtomFeedTypeName
    ];

    private static readonly HashAlgorithmName[] SupportedSignatureAlgorithms = [
        HashAlgorithmName.SHA1,
        HashAlgorithmName.SHA256,
        HashAlgorithmName.SHA384,
        HashAlgorithmName.SHA512
    ];

    public record ValidationResult([property: MemberNotNullWhen(true, nameof(ValidationResult.BodyBytes))] bool IsValid, byte[]? BodyBytes);

    public async Task<ValidationResult> TryValidateAuthenticatedContentDistribution(HttpRequest request, string secret)
    {
        if (!TryValidateContentDistributionHeaders(request.ContentType, request.Headers))
        {
            LogHeaderValidationFailed();
            return new ValidationResult(false, null);
        }

        if (!(request.Headers.TryGetValue("X-Hub-Signature", out var signatureHeaders)
            && !string.IsNullOrWhiteSpace(signatureHeaders)
            && signatureHeaders.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string signatureHeader
            && !string.IsNullOrWhiteSpace(signatureHeader)))
        {
            LogMissingSignatureHeader();
            return new ValidationResult(false, null);
        }

        var headerParts = signatureHeader.Split('=', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (headerParts.Length != 2)
        {
            LogSignatureParseError(signatureHeaders.ToString());
            return new ValidationResult(false, null);
        }

        HashAlgorithmName algoName = new(headerParts[0].ToUpper());

        if (!SupportedSignatureAlgorithms.Contains(algoName))
        {
            LogUnsupportedHashAlgo(headerParts[0]);
            return new ValidationResult(false, null);
        }

        var bodyBytes = await request.Body.ReadAllBytesAsync();

        using var hmac = CreateHmac(algoName, secret);
        using MemoryStream bodyStream = new(bodyBytes, writable: false);

        if (!await VerifyMessage(bodyStream, headerParts[1], hmac))
        {
            LogSignatureValidationFailed();
            return new ValidationResult(false, null);
        }

        return new ValidationResult(true, bodyBytes);
    }

    public async Task<ValidationResult> TryValidateContentDistribution(HttpRequest request)
    {
        if (!TryValidateContentDistributionHeaders(request.ContentType, request.Headers))
        {
            LogHeaderValidationFailed();
            return new ValidationResult(false, null);
        }

        var bodyBytes = await request.Body.ReadAllBytesAsync();

        using MemoryStream bodyStream = new(bodyBytes, writable: false);

        return new ValidationResult(true, bodyBytes);
    }

    public bool TryValidateContentDistributionHeaders(string? contentType, IHeaderDictionary headers)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            LogMissingContentType();
            return false;
        }

        if (!SupportedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            LogUnsupportedContentType(contentType);
            return false;
        }

        if (headers.Link.Count == 0)
        {
            LogMissingLinkHeader();
            return false;
        }

        if (!headers.Link.Any(l => l?.Contains("rel=hub") ?? false))
        {
            LogMissingHubLink(headers.Link.ToString());
            return false;
        }

        if (!headers.Link.Any(l => l?.Contains("rel=self") ?? false))
        {
            LogMissingSelfLink(headers.Link.ToString());
            return false;
        }

        return true;
    }

    private static async Task<bool> VerifyMessage(Stream messageBytes, string signatureHex, HMAC algo, CancellationToken ct = default)
    {
        var hashBytes = await algo.ComputeHashAsync(messageBytes, ct).ConfigureAwait(false);

        var signatureBytes = Convert.FromHexString(signatureHex);

        return CryptographicOperations.FixedTimeEquals(hashBytes, signatureBytes);
    }

    public static HMAC CreateHmac(HashAlgorithmName algoName, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);

        return algoName switch
        {
            { } when algoName == HashAlgorithmName.SHA1 => new HMACSHA1(keyBytes),
            { } when algoName == HashAlgorithmName.SHA256 => new HMACSHA256(keyBytes),
            { } when algoName == HashAlgorithmName.SHA384 => new HMACSHA384(keyBytes),
            { } when algoName == HashAlgorithmName.SHA512 => new HMACSHA512(keyBytes),
            _ => throw new NotImplementedException() // See ref: SupportedSignatureAlgorithms
        };
    }


}