namespace NMAC.Events;

public record IngestYTVideoFeed(byte[] FeedBody, Uri TopicUri, DateTimeOffset ReceivedAtUtc);