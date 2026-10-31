namespace Midnight.ApiServer.Core.Configuration;

/// <summary>
/// Panel configuration stored in storage/config.json. Every value can be overridden with
/// environment variables using the "MIDNIGHT_" prefix, e.g. MIDNIGHT_Database__Provider=PostgreSQL.
/// </summary>
public class AppConfiguration
{
    public string AppName { get; set; } = "Midnight";

    /// <summary>Public url of the panel, used for links in mails and the daemon setup command</summary>
    public string PublicUrl { get; set; } = "http://localhost:5220";

    public DatabaseConfiguration Database { get; set; } = new();
    public AuthenticationConfiguration Authentication { get; set; } = new();
    public SecurityConfiguration Security { get; set; } = new();
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
