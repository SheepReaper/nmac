namespace NMAC.Ui.LiveStreams;

public enum SuperChatSort
{
    UsdDescThenOldest = 0,
    AuthorThenOldest = 1,
    ArrivalOrder = 2
}

public sealed record LiveStreamListItem(
    string VideoId,
    string? Title,
    string? ChannelId,
    bool IsLive,
    int SuperChatCount,
    decimal TotalUsd,
    DateTimeOffset? LastSuperChatAt,
    DateTimeOffset? LastVideoUpdateAt
);

public sealed record TopDonatorRow(
    string DonatorKey,
    string DisplayName,
    decimal TotalUsd,
    int SuperChatCount,
    IReadOnlyList<string> Aliases
);

public sealed record LiveSuperChatRow(
    string MessageId,
    string VideoId,
    string LiveChatId,
    string AuthorKey,
    string AuthorDisplayName,
    long AmountMicros,
    string? Currency,
    string? AmountDisplayString,
    string? MessageContent,
    DateTimeOffset? PublishedAt,
    int AuthorOrdinal,
    decimal? UsdEquivalent,
    IReadOnlyList<string> AuthorAliases
);

public sealed record LiveStreamDashboard(
    string VideoId,
    string? Title,
    string? ChannelId,
    bool IsLive,
    int TotalSuperChatCount,
    decimal TotalUsd,
    double ArrivalRatePerMinute,
    DateTimeOffset? FirstSuperChatAt,
    DateTimeOffset? LastSuperChatAt,
    IReadOnlyList<TopDonatorRow> TopDonators,
    IReadOnlyList<LiveSuperChatRow> SuperChats
);

public sealed record LiveStreamUpdate(
    string VideoId,
    DateTimeOffset OccurredAtUtc,
    int NewSuperChatsPersisted
);
