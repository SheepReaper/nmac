namespace NMAC.Events;

public record LiveStreamFound(string VideoId, string LiveChatId, string? TraceParent = null);