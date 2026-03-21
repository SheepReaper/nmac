using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NMAC.LiveStreams;

file sealed record FrankfurterLatestResponse(
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("base")] string Base,
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("rates")] IReadOnlyDictionary<string, decimal> Rates
);

public sealed class CurrencyConversionService(
        IHttpClientFactory httpClientFactory,
        TimeProvider tp
)
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(6);
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromMinutes(5);

    // Rate cache: currency code → USD per 1 unit of that currency (null = failed/unknown)
    private readonly ConcurrentDictionary<string, decimal?> _rateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _ratesLoadLock = new(1, 1);
    private long _cacheExpiresAtUtcTicks;

    /// <summary>Returns how many USD equal 1 unit of <paramref name="currency"/>, or null if unavailable.</summary>
    public async Task<decimal?> GetRateToUsdAsync(string currency, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return null;

        var normalized = currency.Trim().ToUpperInvariant();

        if (string.Equals(normalized, "USD", StringComparison.OrdinalIgnoreCase))
            return 1m;

        if (IsCacheFresh() && _rateCache.TryGetValue(normalized, out var cached))
            return cached;

        await EnsureRatesLoadedAsync(ct);

        if (_rateCache.TryGetValue(normalized, out cached))
            return cached;

        _rateCache[normalized] = null;
        return null;
    }

    private async Task EnsureRatesLoadedAsync(CancellationToken ct)
    {
        if (IsCacheFresh())
            return;

        await _ratesLoadLock.WaitAsync(ct);

        try
        {
            if (IsCacheFresh())
                return;

            var httpClient = httpClientFactory.CreateClient("Frankfurter");
            var response = await httpClient.GetFromJsonAsync<FrankfurterLatestResponse>("v1/latest?base=USD", _jsonOptions, ct);

            if (response?.Rates is null)
            {
                _rateCache.TryAdd("USD", 1m);
                SetCacheExpiry(FailureRetryDelay);
                return;
            }

            var refreshedRates = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase)
            {
                ["USD"] = 1m
            };

            foreach (var pair in response.Rates)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
                    continue;

                var quote = pair.Key.Trim().ToUpperInvariant();

                // Endpoint gives quote-per-USD; we need USD-per-quote for ranking conversions.
                refreshedRates[quote] = 1m / pair.Value;
            }

            _rateCache.Clear();

            foreach (var pair in refreshedRates)
                _rateCache[pair.Key] = pair.Value;

            SetCacheExpiry(CacheLifetime);
        }
        catch
        {
            _rateCache.TryAdd("USD", 1m);
            SetCacheExpiry(FailureRetryDelay);
        }
        finally
        {
            _ratesLoadLock.Release();
        }
    }

    private bool IsCacheFresh() => tp.GetUtcNow().Ticks < Interlocked.Read(ref _cacheExpiresAtUtcTicks);

    private void SetCacheExpiry(TimeSpan lifetime) => Interlocked.Exchange(
        ref _cacheExpiresAtUtcTicks,
        tp.GetUtcNow().Add(lifetime).Ticks);
}
