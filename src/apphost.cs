#pragma warning disable ASPIRECOMPUTE003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREDOCKERFILEBUILDER001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREPIPELINES003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

#:sdk Aspire.AppHost.Sdk@13.2.0

#:package Aspire.Hosting.PostgreSQL
#:package Aspire.Hosting.DevTunnels
#:package Aspire.Hosting.Docker
#:package Shirubasoft.Aspire.Cloudflared

#:property UserSecretsId=a102d659-e501-48d3-97ed-f207403f65aa

#:project ./server/NMAC.csproj

using Microsoft.Extensions.Hosting;

using Aspire.Cloudflared;
using Aspire.Hosting.Publishing;
var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose")
    .WithDashboard(false);

var registry = builder.AddContainerRegistry("registry", "scr.sheepreaper.xyz", "nmac");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgWeb(pgweb => pgweb.WithHostPort(8081));

var appDb = postgres.AddDatabase("app-db");

var devUsername = builder.AddParameter("dev-access-username", new ConstantParameterDefault("admin"), persist: true);
var devPassword = builder.AddParameter("dev-access-password", new GenerateParameterDefault(), true, true);
var ytApiKey = builder.AddParameter("yt-api-key", secret: true);

var api = builder.AddProject<Projects.NMAC>("api")
    // So that we can have curl and wget to do docker healthchecks
    .WithDockerfileBaseImage(runtimeImage: "mcr.microsoft.com/dotnet/aspnet:10.0-alpine")
    .WithReference(appDb)
    .WaitFor(appDb)
    .WithEnvironment("DeveloperBasicAuth__Username", devUsername)
    .WithEnvironment("DeveloperBasicAuth__Password", devPassword)
    .WithEnvironment("YTClient__ApiKey", ytApiKey)
    .WithEnvironment("DataProtection__KeyRingPath", "/data-protection-keys")
    .WithHttpEndpoint(8080, name: "devtunnel") // ms devtunnel
    .WithHttpEndpoint(80, name: "http") // cloudflared
    .WithContainerRegistry(registry)
    .WithContainerBuildOptions(options =>
    {
        options.ImageFormat = ContainerImageFormat.Oci;
        options.TargetPlatform = ContainerTargetPlatform.AllLinux;
    })
    .PublishAsDockerComposeService((_, service) =>
    {
        service.Healthcheck = new()
        {
            Interval = "5s",
            StartPeriod = "15s",
            Timeout = "2s",
            Test = ["CMD", "wget", "--spider", "-q", "http://localhost:80/health"]
        };
    });

var tunnel = builder.AddDevTunnel("dev-tunnel", "nmac")
    .WithAnonymousAccess()
    .WithReference(api);

api.WithReference(api, tunnel);

if (builder.Environment.IsProduction())
{
    var publicHostname = builder.AddParameter("public-hostname");

    var cfTunnel = builder.AddCloudflareTunnel("nmac-cf-tunnel"); // Is also the tunnel name in cloudflare, so it needs to be unique across your account, not just this project

    cfTunnel.WithImageTag("2026.3.0")
        .PublishAsDockerComposeService((resource, service) =>
        {
            service.Name = resource.Name;
        });

    if (await publicHostname.Resource.GetValueAsync(CancellationToken.None) is not string hostname || string.IsNullOrWhiteSpace(hostname))
    {
        throw new InvalidOperationException("Public hostname must be provided in production environment");
    }

    api.WithCloudflareTunnel(cfTunnel, hostname: hostname, endpointName: "http");
}


if (builder.Environment.IsDevelopment())
{
    api.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
}

await builder.Build().RunAsync();

internal sealed class ConstantParameterDefault(Func<string> valueGetter) : ParameterDefault
{
    private string? _value;

    public ConstantParameterDefault(string value) : this(() => value) { }

    public override string GetDefaultValue() => _value ??= valueGetter();

    public override void WriteToManifest(ManifestPublishingContext context)
    {
        context.Writer.WriteString("value", GetDefaultValue());
    }
}