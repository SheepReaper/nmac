using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using NMAC.Core;

namespace NMAC.LiveStreams;

public sealed class AvatarProxyService(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IServiceScopeFactory scopeFactory,
    TimeProvider tp,
    ILogger<AvatarProxyService> logger)
{
    public const string HttpClientName = "AvatarProxy";

    private const int MaxConcurrentRemoteFetches = 4;
    private static readonly TimeSpan SuccessTtl = TimeSpan.FromDays(1);
    private static readonly TimeSpan FailureTtl = TimeSpan.FromMinutes(10);
    private static readonly SemaphoreSlim RemoteFetchGate = new(MaxConcurrentRemoteFetches, MaxConcurrentRemoteFetches);
    private static readonly ConcurrentDictionary<string, Task<AvatarCacheEntry>> InflightFetches = new(StringComparer.Ordinal);

    public async Task<AvatarCacheEntry> GetAvatarAsync(string sourceUrl, CancellationToken ct = default)
    {
        if (!TryNormalizeUrl(sourceUrl, out var normalizedUrl))
            return AvatarCacheEntry.Missing;

        var cacheKey = BuildCacheKey(normalizedUrl);

        if (cache.TryGetValue(cacheKey, out AvatarCacheEntry? cached) && cached is not null)
            return cached;

        var persisted = await TryGetPersistedEntryAsync(cacheKey, ct);
        if (persisted is not null)
        {
            cache.Set(cacheKey, persisted.Value.Entry, persisted.Value.Ttl);
            return persisted.Value.Entry;
        }

        var fetchTask = InflightFetches.GetOrAdd(cacheKey, _ => FetchAndCacheAsync(cacheKey, normalizedUrl));

        try
        {
            return await fetchTask.WaitAsync(ct);
        }
        finally
        {
            if (fetchTask.IsCompleted)
                InflightFetches.TryRemove(cacheKey, out _);
        }
    }

    private async Task<AvatarCacheEntry> FetchAndCacheAsync(string cacheKey, string normalizedUrl)
    {
        await RemoteFetchGate.WaitAsync();

        try
        {
            if (cache.TryGetValue(cacheKey, out AvatarCacheEntry? cached) && cached is not null)
                return cached;

            var persisted = await TryGetPersistedEntryAsync(cacheKey, CancellationToken.None);
            if (persisted is not null)
            {
                cache.Set(cacheKey, persisted.Value.Entry, persisted.Value.Ttl);
                return persisted.Value.Entry;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, normalizedUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

            var client = httpClientFactory.CreateClient(HttpClientName);

            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);

                if (!response.IsSuccessStatusCode)
                {
                    var missing = AvatarCacheEntry.Missing;
                    await PersistCacheEntryAsync(cacheKey, normalizedUrl, missing, FailureTtl, CancellationToken.None);
                    cache.Set(cacheKey, missing, FailureTtl);

                    if ((int)response.StatusCode == 429)
                        logger.LogInformation("Avatar upstream throttled for {AvatarUrl}", normalizedUrl);

                    return missing;
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (string.IsNullOrWhiteSpace(mediaType) || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    var missing = AvatarCacheEntry.Missing;
                    await PersistCacheEntryAsync(cacheKey, normalizedUrl, missing, FailureTtl, CancellationToken.None);
                    cache.Set(cacheKey, missing, FailureTtl);
                    return missing;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);
                var entry = new AvatarCacheEntry(bytes, mediaType);
                await PersistCacheEntryAsync(cacheKey, normalizedUrl, entry, SuccessTtl, CancellationToken.None);
                cache.Set(cacheKey, entry, SuccessTtl);

                return entry;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed fetching avatar from {AvatarUrl}", normalizedUrl);
                var missing = AvatarCacheEntry.Missing;
                await PersistCacheEntryAsync(cacheKey, normalizedUrl, missing, FailureTtl, CancellationToken.None);
                cache.Set(cacheKey, missing, FailureTtl);
                return missing;
            }
        }
        finally
        {
            RemoteFetchGate.Release();
        }
    }

    private static bool TryNormalizeUrl(string sourceUrl, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme is not ("http" or "https"))
            return false;

        normalizedUrl = uri.ToString();
        return true;
    }

    private async Task<(AvatarCacheEntry Entry, TimeSpan Ttl)?> TryGetPersistedEntryAsync(string cacheKey, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = tp.GetUtcNow();

        var item = await db.AvatarCacheItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CacheKey == cacheKey, ct);

        if (item is null)
            return null;

        if (item.ExpiresAt <= now)
            return null;

        var ttl = item.ExpiresAt - now;
        if (ttl <= TimeSpan.Zero)
            return null;

        var entry = item.IsMissing || item.Content is null || string.IsNullOrWhiteSpace(item.ContentType)
            ? AvatarCacheEntry.Missing
            : new AvatarCacheEntry(item.Content, item.ContentType);

        return (entry, ttl);
    }

    private async Task PersistCacheEntryAsync(
        string cacheKey,
        string normalizedUrl,
        AvatarCacheEntry entry,
        TimeSpan ttl,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = tp.GetUtcNow();

        var item = await db.AvatarCacheItems
            .FirstOrDefaultAsync(x => x.CacheKey == cacheKey, ct);

        if (item is null)
        {
            item = new AvatarCacheItem
            {
                CacheKey = cacheKey,
                SourceUrl = normalizedUrl
            };

            db.AvatarCacheItems.Add(item);
        }

        item.SourceUrl = normalizedUrl;
        item.ContentType = entry.HasContent ? entry.ContentType : null;
        item.Content = entry.HasContent ? entry.Content : null;
        item.IsMissing = !entry.HasContent;
        item.CachedAt = now;
        item.ExpiresAt = now.Add(ttl);

        await db.SaveChangesAsync(ct);
    }

    private static string BuildCacheKey(string normalizedUrl)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedUrl));
        return Convert.ToHexString(bytes);
    }
}

public sealed record AvatarCacheEntry(byte[] Content, string ContentType)
{
    public static AvatarCacheEntry Missing { get; } = new([], string.Empty);

    public bool HasContent => Content.Length > 0 && !string.IsNullOrWhiteSpace(ContentType);
}