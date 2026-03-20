using System.Diagnostics;

namespace NMAC.Videos;

public static class Telemetry
{
    public const string SourceName = "SheepReaper.NMAC.Videos";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}