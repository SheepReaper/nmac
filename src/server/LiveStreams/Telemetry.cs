using System.Diagnostics;

namespace NMAC.LiveStreams;

// Per-module telemetry keeps source ownership and naming aligned with each bounded context.
public static class Telemetry
{
    public const string SourceName = "SheepReaper.NMAC.LiveStreaming";
    public const string MeterName = "NMAC.LiveStreams.LiveChat";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}