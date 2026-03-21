using System.Diagnostics;

namespace NMAC.Videos;

// Per-module telemetry keeps source ownership and naming aligned with each bounded context.
public static class Telemetry
{
    public const string SourceName = "SheepReaper.NMAC.Videos";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}