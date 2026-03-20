using NMAC;
using NMAC.Subscriptions;
const string enforcerChannelId = "UCM-eRxEc_TutiPIbOS1YYbw";

string[] ytTopics = [
  enforcerChannelId,
];

string[] seedChannelHandles = [
  "@EnforcerOfficial",
];

ytTopics = [.. ytTopics.Select(id => string.Format(SubscriptionService.ChannelTopicTemplate, id))];

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
  builder.WebHost.UseStaticWebAssets();

builder.AddNmacServerServices(ytTopics, seedChannelHandles);

var app = builder.Build();

app.MapNmacServer();

await app.MigrateNmacDatabaseAsync();

await app.RunAsync();
