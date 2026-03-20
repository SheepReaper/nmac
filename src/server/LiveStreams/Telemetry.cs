using System.Diagnostics;

namespace NMAC.LiveStreams;

public static class Telemetry
{
    public const string SourceName = "SheepReaper.NMAC.LiveStreaming";
    public const string MeterName = "NMAC.LiveStreams.LiveChat";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}