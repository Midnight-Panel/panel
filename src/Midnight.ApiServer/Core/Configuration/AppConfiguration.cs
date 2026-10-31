using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Core.Configuration;

/// <summary>
/// Panel configuration stored in storage/config.json. Every value can be overridden with
/// environment variables using the "MIDNIGHT_" prefix, e.g. MIDNIGHT_Database__Provider=PostgreSQL.
/// </summary>
public class AppConfiguration
{
    public string AppName { get; set; } = "Midnight";

    /// <summary>Short description or slogan of the panel</summary>
    public string Slogan { get; set; } = "Game Server Management";

    /// <summary>Public url of the panel, used for links in mails and the demon setup command</summary>
    public string PublicUrl { get; set; } = "http://localhost:5220";

    /// <summary>Default language of the panel, e.g. 'de' or 'en'</summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>Default time zone of the panel</summary>
    public string TimeZone { get; set; } = "UTC";

    public DatabaseConfiguration Database { get; set; } = new();
    public AuthenticationConfiguration Authentication { get; set; } = new();
    public SecurityConfiguration Security { get; set; } = new();
    public ApiConfiguration Api { get; set; } = new();
    public FeaturesConfiguration Features { get; set; } = new();
    public FooterConfiguration Footer { get; set; } = new();
    public ServersConfiguration Servers { get; set; } = new();
}

public class ServersConfiguration
{
    /// <summary>Max size of one file upload request (all files together), streamed to the demon. Default 1 GiB.</summary>
    public long MaxUploadBytes { get; set; } = 1024L * 1024 * 1024;
}

public class DatabaseConfiguration
{
    /// <summary>"Sqlite" or "PostgreSQL"</summary>
    public string Provider { get; set; } = DatabaseProviders.Sqlite;

    /// <summary>Empty for Sqlite means storage/midnight.db</summary>
    public string ConnectionString { get; set; } = "";
}

public static class DatabaseProviders
{
    public const string Sqlite = "Sqlite";
    public const string PostgreSql = "PostgreSQL";
}

public class AuthenticationConfiguration
{
    public bool EnableRegistration { get; set; } = true;
    public int AccessTokenMinutes { get; set; } = 10;
    public int RefreshTokenDays { get; set; } = 30;
    public int ImpersonationMinutes { get; set; } = 30;
    public int PasswordResetMinutes { get; set; } = 60;

    /// <summary>Max login, register and password reset requests per ip and minute</summary>
    public int RateLimitPerMinute { get; set; } = 10;
}

public class SecurityConfiguration
{
    /// <summary>HMAC key for access tokens. Generated on first start, never logged or exported.</summary>
    public string JwtSecret { get; set; } = "";

    /// <summary>Origins allowed to call the api from a browser. The bundled client is same origin and needs none.</summary>
    public List<string> CorsOrigins { get; set; } = [];

    /// <summary>
    /// Networks of reverse proxies whose X-Forwarded-* headers are trusted, e.g. "172.30.0.0/16".
    /// Loopback is always trusted.
    /// </summary>
    public List<string> TrustedProxies { get; set; } = [];
}

public class ApiConfiguration
{
    /// <summary>Global toggle to enable or disable user API keys and API access for regular users.</summary>
    public bool UserApiEnabled { get; set; } = true;
}

public class FeaturesConfiguration
{
    /// <summary>When true, displays maintenance banner and restricts regular user access.</summary>
    public bool MaintenanceMode { get; set; } = false;

    /// <summary>Whether regular users can create new game servers.</summary>
    public bool ServerCreationEnabled { get; set; } = true;

    /// <summary>Telemetry and diagnostic reporting toggle.</summary>
    public bool TelemetryEnabled { get; set; } = false;
}

public class FooterConfiguration
{
    public List<FooterLinkItem> Links { get; set; } =
    [
        new() { Id = "api-ref", Label = "API Reference", Url = "/api/reference", NewTab = true },
        new() { Id = "github", Label = "GitHub", Url = "https://github.com/Midnight-Panel/panel", NewTab = true }
    ];
}
