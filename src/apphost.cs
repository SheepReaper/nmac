#pragma warning disable ASPIRECOMPUTE003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREPIPELINES003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#:sdk Aspire.AppHost.Sdk@13.1.3

#:package Aspire.Hosting.Azure.PostgreSQL
#:package Aspire.Hosting.DevTunnels
#:package Aspire.Hosting.Docker
#:package Shirubasoft.Aspire.Cloudflared

#:property UserSecretsId=a102d659-e501-48d3-97ed-f207403f65aa

#:project ./server/NMAC.csproj

using Aspire.Cloudflared;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

var registry = builder.AddContainerRegistry("registry", "scr.sheepreaper.xyz", "nmac");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("apphost-e0df74204a-postgres-data")
    .WithContainerName("postgres-e0df7420")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgWeb(pgweb => pgweb.WithHostPort(8081));

var appDb = postgres.AddDatabase("app-db");

var devUsername = builder.AddParameter("dev-access-username", new ConstantParameterDefault("admin"), persist: true);
var devPassword = builder.AddParameter("dev-access-password", new GenerateParameterDefault(), true, true);
var ytApiKey = builder.AddParameter("yt-api-key", secret: true);

var api = builder.AddProject<Projects.NMAC>("api")
    .WithReference(appDb)
    .WaitFor(appDb)
    .WithEnvironment("DeveloperBasicAuth__Username", devUsername)
    .WithEnvironment("DeveloperBasicAuth__Password", devPassword)
    .WithEnvironment("YTClient__ApiKey", ytApiKey)
    .WithEnvironment("DataProtection__KeyRingPath", "/data-protection-keys")
    .WithHttpEndpoint(8080, name: "devtunnel") // devtunnel
    .WithHttpEndpoint(80, name: "http") // cloudflared
    .WithContainerRegistry(registry)
    .WithContainerBuildOptions(options =>
    {
        options.ImageFormat = ContainerImageFormat.Oci;
        options.TargetPlatform = ContainerTargetPlatform.AllLinux;
    });

var tunnel = builder.AddDevTunnel("subscriber-tunnel", "nmac")
    .WithAnonymousAccess()
    .WithReference(api);

api.WithReference(api, tunnel);

var cfTunnel = builder.Environment.IsProduction()
    ? builder.AddCloudflareTunnel("tunnel")
    : builder.AddCloudflareTunnel("nmac-dev");

cfTunnel.WithImageTag("2026.3.0")
    .PublishAsDockerComposeService((resource, service) =>
    {
        service.Name = resource.Name;
    });

api.WithCloudflareTunnel(cfTunnel, hostname: builder.Environment.IsProduction() ? "nevermac.com" : "dev.nevermac.com", endpointName: "http");

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