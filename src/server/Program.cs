using NMAC;
var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
    builder.WebHost.UseStaticWebAssets();

builder.AddNmacServerServices();

var app = builder.Build();

app.MapNmacServer();

await app.MigrateNmacDatabaseAsync();

await app.RunAsync();
