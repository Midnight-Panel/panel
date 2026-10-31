using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Mail;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Account.Services;
using Midnight.ApiServer.Features.Auth.Http;
using Midnight.ApiServer.Features.Auth.Services;
using Midnight.ApiServer.Features.Branding.Services;
using Midnight.ApiServer.Features.Databases.Services;
using Midnight.ApiServer.Features.Extensions.Services;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.ApiServer.Features.Servers.Services;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.ApiServer.Features.SystemSettings.Services;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared.Http;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configuration: storage/config.json overridable with MIDNIGHT_ environment variables

var storagePaths = new StoragePaths(ConfigurationBootstrapper.GetStoragePath(builder.Environment.ContentRootPath));
var configPath = ConfigurationBootstrapper.EnsureConfigFile(storagePaths.Root);

builder.Configuration
    .AddJsonFile(configPath, optional: false, reloadOnChange: true)
    .AddEnvironmentVariables("MIDNIGHT_");

var configuration = builder.Configuration.Get<AppConfiguration>() ?? new AppConfiguration();

builder.Services.Configure<AppConfiguration>(builder.Configuration);
builder.Services.AddSingleton(storagePaths);
builder.Services.AddSingleton(TimeProvider.System);

// Logging

builder.Services.AddSerilog((_, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(storagePaths.Logs, "panel-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

// Core services

builder.Services.AddDataProtection()
    .SetApplicationName("Midnight")
    .PersistKeysToFileSystem(new DirectoryInfo(storagePaths.Keys));

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMidnightDatabase(configuration, storagePaths.Root);
builder.Services.AddMidnightAuthentication(configuration);
builder.Services.AddSingleton<IMailSender, LogMailSender>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(options => JsonDefaults.Apply(options.JsonSerializerOptions));

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Midnight Panel API";
        document.Info.Description = "Authenticate with \"Authorization: Bearer <access token or api key>\".";
        return Task.CompletedTask;
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(AuthController.RateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = configuration.Authentication.RateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests, try again in a minute"
        }, cancellationToken);
    };
});

if (configuration.Security.CorsOrigins.Count > 0)
{
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(configuration.Security.CorsOrigins.ToArray())
        .AllowAnyHeader()
        .AllowAnyMethod()));
}

// Loopback proxies are always trusted, further networks (e.g. the docker network of a reverse proxy) via Security.TrustedProxies
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    foreach (var network in configuration.Security.TrustedProxies)
    {
        if (!System.Net.IPNetwork.TryParse(network, out var parsed))
            throw new InvalidOperationException($"Security.TrustedProxies: '{network}' is not a valid network like 172.30.0.0/16");

        options.KnownIPNetworks.Add(parsed);
    }
});

// Features

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TotpService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<RoleService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<AvatarService>();
builder.Services.AddScoped<NodeService>();
builder.Services.AddScoped<AllocationService>();
builder.Services.AddScoped<NodeClient>();
builder.Services.AddScoped<IShardCollectionService, ShardCollectionService>();
builder.Services.AddScoped<IShardService, ShardService>();
builder.Services.AddScoped<IShardVariableService, ShardVariableService>();
builder.Services.AddScoped<IShardImportExportService, ShardImportExportService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IBrandingService, BrandingService>();
builder.Services.AddScoped<IExtensionService, ExtensionService>();
builder.Services.AddScoped<ServerService>();
builder.Services.AddScoped<ConsoleProxy>();
builder.Services.AddSingleton<ConsoleTicketService>();
builder.Services.AddSingleton<DownloadTicketService>();
builder.Services.AddScoped<ServerAccessService>();
builder.Services.AddScoped<ActivityService>();
builder.Services.AddScoped<SubuserService>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<ServerNetworkService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<ScheduleRunner>();
builder.Services.AddSingleton<ScheduleDispatcher>();
builder.Services.AddHostedService<ScheduleWorker>();
builder.Services.AddSingleton<IDatabaseExecutor, MySqlDatabaseExecutor>();
builder.Services.AddScoped<DatabaseHostService>();
builder.Services.AddScoped<ServerDatabaseService>();

builder.Services.AddHttpClient(NodeClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5));

// Uploads, downloads and long file operations: no overall timeout (callers use their own), but don't hang on connecting
builder.Services.AddHttpClient(NodeClient.StreamingHttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(10) });

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseSerilogRequestLogging();

// Revalidate static files on every load (cheap 304 via ETag) so updates never show stale css, js or translations
var staticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache"
};

app.UseStaticFiles(staticFileOptions);

// Server console (/api/servers/{uuid}/console), proxied to the demon
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

app.UseRouting();

if (configuration.Security.CorsOrigins.Count > 0)
    app.UseCors();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness check for the installer, the cli and container health checks
app.MapGet("/api/health", async (DataContext context, CancellationToken cancellationToken) =>
    await context.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ok" })
        : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Database not reachable"))
    .AllowAnonymous()
    .ExcludeFromDescription();

app.MapOpenApi("/api/openapi/{documentName}.json");
app.MapScalarApiReference("/api/reference", options => options
    .WithTitle("Midnight Panel API")
    .WithOpenApiRoutePattern("/api/openapi/{documentName}.json"));

// Unknown api routes must not fall through to the client
app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Endpoint not found"));
app.MapFallbackToFile("index.html", staticFileOptions);

await app.MigrateDatabaseAsync();

// "create-admin" is used by the installer: creates the first administrator and exits
if (args.Length > 0 && args[0] == "create-admin")
    return await AdminCommand.RunAsync(app, args);

await app.RunAsync();
return 0;

// Accessible for WebApplicationFactory based tests
public partial class Program;
