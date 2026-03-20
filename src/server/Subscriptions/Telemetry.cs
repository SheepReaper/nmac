using System.Diagnostics;

namespace NMAC.Subscriptions;

public static class Telemetry
{
    public const string SourceName = "SheepReaper.NMAC.Subscriptions";
    public static readonly ActivitySource ActivitySource = new(SourceName);
}