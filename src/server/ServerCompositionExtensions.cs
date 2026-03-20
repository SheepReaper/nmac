using System.Xml.Serialization;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

using Grpc.Core;
using Grpc.Net.Client;

using NMAC.Components;
using NMAC.Core;
using NMAC.LiveStreams;
using NMAC.Subscriptions;
using NMAC.Subscriptions.WebSub;
using NMAC.Subscriptions.WebSub.Atom;
using NMAC.Ui;
using NMAC.Ui.LiveStreams;
using NMAC.Videos.YTRestClient;

using ProtoBuf.Grpc.Client;

using Refit;

using Wolverine;

using youtube.api.v3;

namespace NMAC;

public static class ServerCompositionExtensions
{
  public static WebApplicationBuilder AddNmacServerServices(
    this WebApplicationBuilder builder,
    string[] ytTopics,
    string[] seedChannelHandles)
  {
    builder.AddServiceDefaults();
    builder.AddNmacPersistence(ytTopics, seedChannelHandles);
    builder.AddNmacMessaging();
    builder.AddNmacYouTubeClients();
    builder.AddNmacTelemetry();
    builder.AddNmacAuth();
    builder.AddNmacDataProtection();
    builder.AddNmacForwardedHeaders();
    builder.AddNmacDomainServices();

    return builder;
  }

  public static WebApplicationBuilder AddNmacDataProtection(this WebApplicationBuilder builder)
  {
    var configuredKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var defaultKeyRingRoot = string.IsNullOrWhiteSpace(appDataPath) ? Path.GetTempPath() : appDataPath;

    var keyRingPath = !string.IsNullOrWhiteSpace(configuredKeyRingPath)
      ? configuredKeyRingPath
      : Path.Combine(defaultKeyRingRoot, "NMAC", "DataProtectionKeys");

    Directory.CreateDirectory(keyRingPath);

    builder.Services.AddDataProtection()
      .SetApplicationName("NMAC")
      .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath));

    return builder;
  }

  public static WebApplicationBuilder AddNmacForwardedHeaders(this WebApplicationBuilder builder)
  {
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
      options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;

      // TODO: Make this configurable so we can pin known proxies.
      options.KnownIPNetworks.Clear();
      options.KnownProxies.Clear();
    });

    return builder;
  }

  public static WebApplicationBuilder AddNmacPersistence(
    this WebApplicationBuilder builder,
    string[] ytTopics,
    string[] seedChannelHandles)
  {
    builder.AddNpgsqlDbContext<AppDbContext>("app-db", driver => { }, contextOptions =>
    {
      contextOptions
        .UseSnakeCaseNamingConvention()
        .UseAsyncSeeding(async (db, _, ct) =>
        {
          var subs = db.Set<Subscription>();

          var missingSubs = ytTopics.Except(subs.Select(s => s.TopicUri.ToString())).Select(m => new Subscription
          {
            TopicUri = new Uri(m),
            Mode = HubMode.Subscribe.ToString(),
            Enabled = true
          });

          await subs.AddRangeAsync(missingSubs, ct);

          if (missingSubs.Any())
            await db.SaveChangesAsync(ct);

          var pollTargets = db.Set<ChannelLivePollTarget>();
          var normalizedSeedHandles = seedChannelHandles
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(ChannelLiveDetectionWorker.NormalizeHandle)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

          var missingPollTargets = normalizedSeedHandles
            .Except(pollTargets.Select(t => t.Handle))
            .Select(handle => new ChannelLivePollTarget
            {
              Handle = handle,
              Enabled = true
            });

          await pollTargets.AddRangeAsync(missingPollTargets, ct);

          if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
        });
    });

    return builder;
  }

  public static WebApplicationBuilder AddNmacMessaging(this WebApplicationBuilder builder)
  {
    builder.UseWolverine(options =>
    {
      options.CodeGeneration.AlwaysUseServiceLocationFor<AppDbContext>();
    });

    return builder;
  }

  public static WebApplicationBuilder AddNmacYouTubeClients(this WebApplicationBuilder builder)
  {
    builder.Services.AddRefitClient<IYouTubeLiveChatApi>()
      .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://www.googleapis.com/youtube/v3"))
      .AddHttpMessageHandler<AuthHeaderHandler>();

    builder.Services.AddOptions<SubscriptionServiceOptions>()
      .Configure(options =>
      {
        options.HubUri = new Uri("https://pubsubhubbub.appspot.com");
        options.CallbackBaseUri = new(builder.Environment.IsProduction()
          ? new("https://nevermac.com") 
          : builder.Configuration.GetValue<Uri>("services:api:http:0") ?? throw new InvalidOperationException("CallbackBaseUri configuration value is required."), "webhooks/youtube/videos/");
      }).ValidateOnStart();

    builder.Services.AddOptions<YTClientOptions>()
      .Bind(builder.Configuration.GetSection("YTClient"))
      .ValidateOnStart();

    builder.Services.AddOptions<YTGrpcClientOptions>()
      .Bind(builder.Configuration.GetSection("YTClient"))
      .ValidateOnStart();

    builder.Services.AddOptions<ChannelLivePollingOptions>()
      .Configure(options => options.Enabled = true)
      .ValidateOnStart();

    builder.Services.AddSingleton(sp =>
    {
      var channel = GrpcChannel.ForAddress("dns:///youtube.googleapis.com", new GrpcChannelOptions
      {
        Credentials = ChannelCredentials.SecureSsl
      });

      return channel.CreateGrpcService<IYouTubeLiveChatStreamList>();
    });

    builder.Services.AddHttpClient(ChannelLiveDetectionWorker.ProbeClientName)
      .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
      {
        AllowAutoRedirect = false
      });

    return builder;
  }

  public static WebApplicationBuilder AddNmacTelemetry(this WebApplicationBuilder builder)
  {
    builder.Services.AddOpenTelemetry()
      .WithMetrics(metrics =>
      {
        metrics.AddMeter(NMAC.LiveStreams.Telemetry.MeterName);
      })
      .WithTracing(tracing =>
      {
        tracing.AddSource(NMAC.Videos.Telemetry.SourceName);
        tracing.AddSource(NMAC.LiveStreams.Telemetry.SourceName);
        tracing.AddSource("Wolverine");
      });

    return builder;
  }

  public static WebApplicationBuilder AddNmacAuth(this WebApplicationBuilder builder)
  {
    builder.Services.Configure<DeveloperBasicAuthOptions>(
      builder.Configuration.GetSection("DeveloperBasicAuth"));

    builder.Services.AddAuthentication("DeveloperBasic")
      .AddScheme<AuthenticationSchemeOptions, DeveloperBasicAuthHandler>("DeveloperBasic", null);

    builder.Services.AddAuthorizationBuilder()
      .AddPolicy("DeveloperEndpointsBasicAuth", policy =>
      {
        policy.AuthenticationSchemes.Add("DeveloperBasic");
        policy.RequireAuthenticatedUser();
      });

    return builder;
  }

  public static WebApplicationBuilder AddNmacDomainServices(this WebApplicationBuilder builder)
  {
    builder.Services.AddRazorComponents()
      .AddInteractiveServerComponents();

    builder.Services.AddSingleton((_) => TimeProvider.System);
    builder.Services.AddSingleton(new XmlSerializer(typeof(Feed)));
    builder.Services.AddSingleton<FeedValidator>();
    builder.Services.AddSingleton<WebSubClient>();
    builder.Services.AddTransient<AuthHeaderHandler>();
    builder.Services.AddSingleton(VersionHelper.Get());

    builder.Services.AddScoped<SubscriptionService>();
    builder.Services.AddScoped<BrowserTimeInterop>();
    builder.Services.AddScoped<LiveChatStreamProcessor>();
    builder.Services.AddScoped<ILiveStreamDashboardQueryService, LiveStreamDashboardQueryService>();

    builder.Services.AddHttpClient("Frankfurter", c =>
      c.BaseAddress = new Uri("https://api.frankfurter.dev/"));
    builder.Services.AddSingleton<CurrencyConversionService>();

    builder.Services.AddSingleton<LiveStreamUpdateNotifier>();
    builder.Services.AddSingleton<ILiveStreamUpdateNotifier>(sp => sp.GetRequiredService<LiveStreamUpdateNotifier>());
    builder.Services.AddSingleton<ILiveStreamUpdatePublisher>(sp => sp.GetRequiredService<LiveStreamUpdateNotifier>());

    builder.Services.AddSingleton<ILiveChatCaptureSignal, LiveChatCaptureSignal>();

    builder.Services.AddAssemblyUseCases(typeof(Program).Assembly);
    builder.Services.AddAssemblyEndpoints(typeof(Program).Assembly);

    builder.Services.AddHostedService<StartupService>();
    builder.Services.AddHostedService<SubscriptionRefreshWorker>();
    builder.Services.AddHostedService<LiveChatCaptureWorker>();
    builder.Services.AddHostedService<ChannelLiveDetectionWorker>();

    return builder;
  }

  public static WebApplication MapNmacServer(this WebApplication app)
  {
    app.UseForwardedHeaders();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseAntiforgery();

    app.MapDefaultEndpoints();
    app.MapRegisteredEndpoints();
    app.MapStaticAssets();

    app.MapRazorComponents<App>()
      .AddAdditionalAssemblies(typeof(NMAC.Ui.Components._Imports).Assembly)
      .AddInteractiveServerRenderMode();

    return app;
  }

  public static async Task MigrateNmacDatabaseAsync(this WebApplication app)
  {
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
  }
}