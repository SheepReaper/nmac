using Microsoft.EntityFrameworkCore;

using NMAC.Core;
using NMAC.Ui.LiveStreams;

namespace NMAC.LiveStreams;

public sealed class LiveStreamDashboardQueryService(AppDbContext db, CurrencyConversionService currencyConverter) : ILiveStreamDashboardQueryService
{
    public async Task<IReadOnlyList<LiveStreamListItem>> GetStreamsAsync(bool includeAllDiscovered = false, CancellationToken ct = default)
    {
        var videos = await db.YTVideos
            .AsNoTracking()
            .Select(v => new
            {
                v.VideoId,
                v.Title,
                v.ChannelId,
                v.UpdatedAt
            })
            .ToListAsync(ct);

        var chatAgg = await db.LiveSuperChats
            .AsNoTracking()
            .GroupBy(c => c.VideoId)
            .Select(g => new
            {
                VideoId = g.Key,
                SuperChatCount = g.Count(),
                TotalUsdMicros = g.Where(c => c.Currency == "USD").Sum(c => (long?)c.AmountMicros) ?? 0,
                LastSuperChatAt = g.Max(c => c.PublishedAt)
            })
            .ToListAsync(ct);

        var liveVideoIds = await db.LiveChatCaptureSessions
            .AsNoTracking()
            .Where(s => s.State == "Requested" || s.State == "Running")
            .Select(s => s.VideoId)
            .Distinct()
            .ToListAsync(ct);

        var liveLookup = liveVideoIds.ToHashSet(StringComparer.Ordinal);
        var videoLookup = videos.ToDictionary(v => v.VideoId, StringComparer.Ordinal);
        var aggLookup = chatAgg.ToDictionary(c => c.VideoId, StringComparer.Ordinal);

        var allVideoIds = videoLookup.Keys
            .Concat(aggLookup.Keys)
            .Distinct(StringComparer.Ordinal);

        var actionableVideoIds = aggLookup.Keys
            .Concat(liveLookup)
            .Distinct(StringComparer.Ordinal);

        var targetVideoIds = includeAllDiscovered
            ? allVideoIds
            : actionableVideoIds;

        var items = targetVideoIds
            .Select(videoId =>
            {
                videoLookup.TryGetValue(videoId, out var video);
                aggLookup.TryGetValue(videoId, out var agg);

                return new LiveStreamListItem(
                    videoId,
                    video?.Title,
                    video?.ChannelId,
                    liveLookup.Contains(videoId),
                    agg?.SuperChatCount ?? 0,
                    MicrosToUsd(agg?.TotalUsdMicros ?? 0),
                    agg?.LastSuperChatAt,
                    video?.UpdatedAt
                );
            })
            .OrderByDescending(x => x.IsLive)
            .ThenByDescending(x => x.LastSuperChatAt ?? x.LastVideoUpdateAt)
            .ThenBy(x => x.Title ?? x.VideoId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return items;
    }

    public async Task<LiveStreamDashboard?> GetStreamDashboardAsync(string videoId, CancellationToken ct = default)
    {
        var video = await db.YTVideos
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.VideoId == videoId, ct);

        var isLive = await db.LiveChatCaptureSessions
            .AsNoTracking()
            .AnyAsync(s => s.VideoId == videoId && (s.State == "Requested" || s.State == "Running"), ct);

        var rows = await db.LiveSuperChats
            .AsNoTracking()
            .Where(c => c.VideoId == videoId)
            .Select(c => new
            {
                c.MessageId,
                c.VideoId,
                c.LiveChatId,
                c.AuthorChannelId,
                c.AuthorDisplayName,
                c.AmountMicros,
                c.Currency,
                c.AmountDisplayString,
                c.MessageContent,
                c.PublishedAt
            })
            .ToListAsync(ct);

        if (video is null && rows.Count == 0)
            return null;

        var authorChannelIds = rows
            .Select(r => r.AuthorChannelId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var aliasHistory = authorChannelIds.Count == 0
            ? []
            : await db.LiveSuperChats
                .AsNoTracking()
                .Where(c => c.AuthorChannelId != null && authorChannelIds.Contains(c.AuthorChannelId))
                .Where(c => c.AuthorDisplayName != null && c.AuthorDisplayName != string.Empty)
                .Select(c => new
                {
                    c.AuthorChannelId,
                    c.AuthorDisplayName,
                    c.PublishedAt
                })
                .ToListAsync(ct);

        var aliasLookup = aliasHistory
            .GroupBy(x => x.AuthorChannelId!, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => BuildOrderedAliases(g.Select(x => (x.AuthorDisplayName, x.PublishedAt))),
                StringComparer.Ordinal);

        // Pre-fetch USD conversion rates for all unique non-USD currencies in this stream
        var uniqueCurrencies = rows
            .Select(r => r.Currency)
            .Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c, "USD", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rateTasks = uniqueCurrencies.ToDictionary(
            c => c!,
            c => currencyConverter.GetRateToUsdAsync(c!, ct),
            StringComparer.OrdinalIgnoreCase);
        await Task.WhenAll(rateTasks.Values);

        var rates = rateTasks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result, StringComparer.OrdinalIgnoreCase);

        decimal? GetUsdEquivalent(string? currency, long amountMicros)
        {
            if (string.IsNullOrWhiteSpace(currency)) return null;
            if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
                return MicrosToUsd(amountMicros);
            return rates.TryGetValue(currency, out var rate) && rate.HasValue
                ? MicrosToUsd(amountMicros) * rate.Value
                : null;
        }

        var ordinalCounters = new Dictionary<string, int>(StringComparer.Ordinal);
        var orderedForOrdinals = rows
            .OrderBy(r => r.PublishedAt)
            .ThenBy(r => r.MessageId)
            .ToList();

        var rowMap = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var row in orderedForOrdinals)
        {
            var authorKey = BuildAuthorKey(row.AuthorChannelId, row.AuthorDisplayName);
            ordinalCounters.TryGetValue(authorKey, out var count);
            count++;
            ordinalCounters[authorKey] = count;
            rowMap[row.MessageId] = count;
        }

        var mappedRows = rows
            .Select(r =>
            {
                var authorKey = BuildAuthorKey(r.AuthorChannelId, r.AuthorDisplayName);
                var usdEquivalent = GetUsdEquivalent(r.Currency, r.AmountMicros);
                var authorAliases = GetAuthorAliases(authorKey, r.AuthorChannelId, r.AuthorDisplayName, aliasLookup);
                return new LiveSuperChatRow(
                    r.MessageId,
                    r.VideoId,
                    r.LiveChatId,
                    authorKey,
                    r.AuthorDisplayName ?? "Unknown",
                    r.AmountMicros,
                    r.Currency,
                    r.AmountDisplayString,
                    r.MessageContent,
                    r.PublishedAt,
                    rowMap.TryGetValue(r.MessageId, out var ordinal) ? ordinal : 1,
                    usdEquivalent,
                    authorAliases
                );
            })
            .OrderByDescending(r => r.UsdEquivalent ?? -1m)
            .ThenBy(r => r.PublishedAt)
            .ThenBy(r => r.MessageId)
            .ToList();

        var totalUsd = MicrosToUsd(rows.Where(r => string.Equals(r.Currency, "USD", StringComparison.OrdinalIgnoreCase)).Sum(r => r.AmountMicros));
        var firstSuperChatAt = rows.MinBy(r => r.PublishedAt)?.PublishedAt;
        var lastSuperChatAt = rows.MaxBy(r => r.PublishedAt)?.PublishedAt;

        var arrivalRatePerMinute = 0d;
        if (firstSuperChatAt.HasValue && lastSuperChatAt.HasValue && lastSuperChatAt > firstSuperChatAt)
        {
            var minutes = (lastSuperChatAt.Value - firstSuperChatAt.Value).TotalMinutes;
            if (minutes > 0)
                arrivalRatePerMinute = rows.Count / minutes;
        }

        var topDonators = mappedRows
            .GroupBy(r => r.AuthorKey)
            .Select(g => new TopDonatorRow(
                g.Key,
                g.SelectMany(x => x.AuthorAliases).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? g.Select(x => x.AuthorDisplayName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? g.Key,
                g.Sum(x => x.UsdEquivalent ?? 0m),
                g.Count(),
                BuildOrderedAliases(g.SelectMany(x => x.AuthorAliases.Select(alias => ((string?)alias, (DateTimeOffset?)null))))))
            .OrderByDescending(x => x.TotalUsd)
            .ThenByDescending(x => x.SuperChatCount)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        return new LiveStreamDashboard(
            videoId,
            video?.Title,
            video?.ChannelId,
            isLive,
            rows.Count,
            totalUsd,
            arrivalRatePerMinute,
            firstSuperChatAt,
            lastSuperChatAt,
            topDonators,
            mappedRows);
    }

    private static decimal MicrosToUsd(long micros) => micros / 1_000_000m;

    private static IReadOnlyList<string> GetAuthorAliases(
        string authorKey,
        string? authorChannelId,
        string? authorDisplayName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> aliasLookup)
    {
        if (!string.IsNullOrWhiteSpace(authorChannelId) && aliasLookup.TryGetValue(authorChannelId, out var aliases))
            return aliases;

        if (!string.IsNullOrWhiteSpace(authorDisplayName))
            return [authorDisplayName.Trim()];

        return [authorKey];
    }

    private static IReadOnlyList<string> BuildOrderedAliases(IEnumerable<(string? Name, DateTimeOffset? PublishedAt)> values)
    {
        var aliases = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (Name, PublishedAt) in values
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var alias = Name!.Trim();
            if (seen.Add(alias))
                aliases.Add(alias);
        }

        return aliases;
    }

    private static string BuildAuthorKey(string? authorChannelId, string? authorDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(authorChannelId))
            return authorChannelId;

        if (!string.IsNullOrWhiteSpace(authorDisplayName))
            return $"name:{authorDisplayName.Trim()}";

        return "unknown";
    }
}
