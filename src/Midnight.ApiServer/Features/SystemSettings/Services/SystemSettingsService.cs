using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.SystemSettings.Services;

public partial class SystemSettingsService : ISystemSettingsService
{
    private readonly DataContext _context;
    private readonly StoragePaths _storagePaths;
    private readonly IOptionsMonitor<AppConfiguration> _configuration;
    private readonly ILogger<SystemSettingsService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [GeneratedRegex(@"(?i)(password|pwd|user id|uid|user)=([^;]+)")]
    private static partial Regex ConnectionStringSecretRegex();

    [GeneratedRegex(@"(?i)bearer\s+[a-z0-9._\-]+")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?i)(token|secret|password|key)=([^\s&"",]+)")]
    private static partial Regex QueryOrFormSecretRegex();

    public SystemSettingsService(
        DataContext context,
        StoragePaths storagePaths,
        IOptionsMonitor<AppConfiguration> configuration,
        ILogger<SystemSettingsService> logger)
    {
        _context = context;
        _storagePaths = storagePaths;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<AdminSettingsResponse> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var config = _configuration.CurrentValue;

        var response = new AdminSettingsResponse
        {
            AppName = config.AppName,
            PublicUrl = config.PublicUrl,
            EnableRegistration = config.Authentication.EnableRegistration,
            UserApiEnabled = config.Api.UserApiEnabled,
            MaintenanceMode = config.Features.MaintenanceMode,
            ServerCreationEnabled = config.Features.ServerCreationEnabled,
            TelemetryEnabled = config.Features.TelemetryEnabled,
            FooterLinks = config.Footer?.Links ?? []
        };

        return Task.FromResult(response);
    }

    public async Task<AdminSettingsResponse> UpdateSettingsAsync(AdminSettingsUpdateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AppName))
            throw HttpApiException.BadRequest("Panel name cannot be empty.");

        if (string.IsNullOrWhiteSpace(request.PublicUrl) || !Uri.TryCreate(request.PublicUrl, UriKind.Absolute, out _))
            throw HttpApiException.BadRequest("Public URL must be a valid absolute URL (e.g. http://localhost:5220).");

        var configPath = Path.Combine(_storagePaths.Root, ConfigurationBootstrapper.ConfigFileName);

        JsonObject root;
        if (File.Exists(configPath))
        {
            var content = await File.ReadAllTextAsync(configPath, cancellationToken);
            root = JsonNode.Parse(content) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = JsonSerializer.SerializeToNode(new AppConfiguration(), JsonOptions)!.AsObject();
        }

        root["AppName"] = request.AppName.Trim();
        root["PublicUrl"] = request.PublicUrl.Trim();

        // Authentication
        if (root["Authentication"] is not JsonObject auth)
        {
            auth = new JsonObject();
            root["Authentication"] = auth;
        }
        auth["EnableRegistration"] = request.EnableRegistration;

        // Api
        if (root["Api"] is not JsonObject api)
        {
            api = new JsonObject();
            root["Api"] = api;
        }
        api["UserApiEnabled"] = request.UserApiEnabled;

        // Features
        if (root["Features"] is not JsonObject features)
        {
            features = new JsonObject();
            root["Features"] = features;
        }
        features["MaintenanceMode"] = request.MaintenanceMode;
        features["ServerCreationEnabled"] = request.ServerCreationEnabled;
        features["TelemetryEnabled"] = request.TelemetryEnabled;

        // Footer
        if (root["Footer"] is not JsonObject footer)
        {
            footer = new JsonObject();
            root["Footer"] = footer;
        }

        // Clean up footer links: ensure unique IDs and valid entries
        var cleanLinks = new List<FooterLinkItem>();
        if (request.FooterLinks != null)
        {
            foreach (var link in request.FooterLinks)
            {
                if (!string.IsNullOrWhiteSpace(link.Label) && !string.IsNullOrWhiteSpace(link.Url))
                {
                    cleanLinks.Add(new FooterLinkItem
                    {
                        Id = string.IsNullOrWhiteSpace(link.Id) ? Guid.NewGuid().ToString("N")[..8] : link.Id.Trim(),
                        Label = link.Label.Trim(),
                        Url = link.Url.Trim(),
                        NewTab = link.NewTab
                    });
                }
            }
        }
        footer["Links"] = JsonSerializer.SerializeToNode(cleanLinks, JsonOptions);

        // Save safely
        await File.WriteAllTextAsync(configPath, root.ToJsonString(JsonOptions), cancellationToken);

        _logger.LogInformation("System settings updated successfully");

        return new AdminSettingsResponse
        {
            AppName = request.AppName.Trim(),
            PublicUrl = request.PublicUrl.Trim(),
            EnableRegistration = request.EnableRegistration,
            UserApiEnabled = request.UserApiEnabled,
            MaintenanceMode = request.MaintenanceMode,
            ServerCreationEnabled = request.ServerCreationEnabled,
            TelemetryEnabled = request.TelemetryEnabled,
            FooterLinks = cleanLinks
        };
    }

    public async Task<byte[]> GenerateTroubleshootingZipAsync(CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            var now = DateTimeOffset.UtcNow;
            var config = _configuration.CurrentValue;
            var version = typeof(SystemSettingsService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

            // 1. README.txt
            var readmeBuilder = new StringBuilder();
            readmeBuilder.AppendLine("================================================================================");
            readmeBuilder.AppendLine("MIDNIGHT PANEL - CONFIDENTIAL TROUBLESHOOTING & DIAGNOSTIC BUNDLE");
            readmeBuilder.AppendLine("================================================================================");
            readmeBuilder.AppendLine($"Generated At:     {now:u}");
            readmeBuilder.AppendLine($"Panel Version:    v{version}");
            readmeBuilder.AppendLine($"Operating System: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
            readmeBuilder.AppendLine($".NET Runtime:     {RuntimeInformation.FrameworkDescription}");
            readmeBuilder.AppendLine();
            readmeBuilder.AppendLine("CONFIDENTIAL NOTICE:");
            readmeBuilder.AppendLine("This diagnostic package is intended exclusively for the official Midnight Panel");
            readmeBuilder.AppendLine("support team. Please only provide this bundle when explicitly requested by a");
            readmeBuilder.AppendLine("verified Midnight support representative.");
            readmeBuilder.AppendLine();
            readmeBuilder.AppendLine("SECURITY & PRIVACY GUARANTEE:");
            readmeBuilder.AppendLine("- All database passwords and connection credentials have been redacted.");
            readmeBuilder.AppendLine("- Secret encryption keys (JWT HMAC secret) have been redacted.");
            readmeBuilder.AppendLine("- Authentication tokens and bearer headers have been redacted.");
            readmeBuilder.AppendLine("================================================================================");

            var readmeEntry = archive.CreateEntry("README.txt", CompressionLevel.Optimal);
            using (var stream = readmeEntry.Open())
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(readmeBuilder.ToString());
            }

            // 2. config.sanitized.json
            var sanitizedConfigJson = GetSanitizedConfigJson();
            var configEntry = archive.CreateEntry("config.sanitized.json", CompressionLevel.Optimal);
            using (var stream = configEntry.Open())
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(sanitizedConfigJson);
            }

            // 3. system-info.json
            var userCount = await _context.Users.CountAsync(cancellationToken);
            var nodeCount = await _context.Nodes.CountAsync(cancellationToken);
            var shardCount = await _context.Shards.CountAsync(cancellationToken);
            var allocationCount = await _context.Allocations.CountAsync(cancellationToken);
            var sessionCount = await _context.UserSessions.CountAsync(cancellationToken);

            TimeSpan uptime;
            try
            {
                uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            }
            catch
            {
                uptime = TimeSpan.Zero;
            }

            var systemInfo = new
            {
                panelVersion = version,
                generatedAt = now,
                system = new
                {
                    os = RuntimeInformation.OSDescription,
                    architecture = RuntimeInformation.OSArchitecture.ToString(),
                    runtime = RuntimeInformation.FrameworkDescription,
                    processUptime = uptime.ToString(@"d\.hh\:mm\:ss"),
                    workingSetMb = Environment.WorkingSet / (1024 * 1024),
                    gcMemoryMb = GC.GetTotalMemory(false) / (1024 * 1024)
                },
                database = new
                {
                    provider = config.Database.Provider
                },
                statistics = new
                {
                    users = userCount,
                    nodes = nodeCount,
                    shards = shardCount,
                    allocations = allocationCount,
                    activeSessions = sessionCount
                },
                features = new
                {
                    registrationEnabled = config.Authentication.EnableRegistration,
                    userApiEnabled = config.Api.UserApiEnabled,
                    maintenanceMode = config.Features.MaintenanceMode,
                    serverCreationEnabled = config.Features.ServerCreationEnabled,
                    telemetryEnabled = config.Features.TelemetryEnabled
                }
            };

            var sysInfoEntry = archive.CreateEntry("system-info.json", CompressionLevel.Optimal);
            using (var stream = sysInfoEntry.Open())
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(JsonSerializer.Serialize(systemInfo, JsonOptions));
            }

            // 4. diagnostics.json
            var dbStopwatch = Stopwatch.StartNew();
            bool dbConnected;
            try
            {
                dbConnected = await _context.Database.CanConnectAsync(cancellationToken);
            }
            catch
            {
                dbConnected = false;
            }
            dbStopwatch.Stop();

            var storageWritable = CheckDirectoryWritable(_storagePaths.Root);
            var logsWritable = CheckDirectoryWritable(_storagePaths.Logs);

            var diagnostics = new
            {
                database = new
                {
                    connected = dbConnected,
                    latencyMs = dbStopwatch.ElapsedMilliseconds
                },
                storage = new
                {
                    rootWritable = storageWritable,
                    logsWritable = logsWritable,
                    keysDirectoryExists = Directory.Exists(_storagePaths.Keys)
                },
                nodes = new
                {
                    total = nodeCount
                }
            };

            var diagEntry = archive.CreateEntry("diagnostics.json", CompressionLevel.Optimal);
            using (var stream = diagEntry.Open())
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                await writer.WriteAsync(JsonSerializer.Serialize(diagnostics, JsonOptions));
            }

            // 5. logs/ directory with sanitized error logs
            var errorLines = new List<string>();

            if (Directory.Exists(_storagePaths.Logs))
            {
                var logFiles = Directory.GetFiles(_storagePaths.Logs, "panel-*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(5)
                    .ToList();

                foreach (var file in logFiles)
                {
                    try
                    {
                        var lines = await File.ReadAllLinesAsync(file.FullName, cancellationToken);
                        var sanitizedLines = new List<string>();

                        foreach (var line in lines)
                        {
                            var sanitized = SanitizeLogLine(line);
                            sanitizedLines.Add(sanitized);

                            if (line.Contains("[ERR]") || line.Contains("[FTL]") || line.Contains("[WRN]") || line.Contains("Exception"))
                            {
                                errorLines.Add($"[{file.Name}] {sanitized}");
                            }
                        }

                        var logEntry = archive.CreateEntry($"logs/{file.Name}", CompressionLevel.Optimal);
                        using (var stream = logEntry.Open())
                        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
                        {
                            await writer.WriteAsync(string.Join(Environment.NewLine, sanitizedLines));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to include log file {File} in troubleshooting bundle", file.Name);
                    }
                }
            }

            // Summary of errors
            var errorSummaryEntry = archive.CreateEntry("logs/errors_summary.txt", CompressionLevel.Optimal);
            using (var stream = errorSummaryEntry.Open())
            await using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                if (errorLines.Count == 0)
                {
                    await writer.WriteLineAsync("No error, warning, or exception entries found in recent log files.");
                }
                else
                {
                    await writer.WriteLineAsync($"=== RECENT ERRORS AND WARNINGS ({errorLines.Count} entries) ===");
                    await writer.WriteLineAsync();
                    foreach (var err in errorLines.Take(500))
                    {
                        await writer.WriteLineAsync(err);
                    }
                }
            }
        }

        return memoryStream.ToArray();
    }

    private string GetSanitizedConfigJson()
    {
        var configPath = Path.Combine(_storagePaths.Root, ConfigurationBootstrapper.ConfigFileName);
        JsonObject root;

        try
        {
            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                root = JsonNode.Parse(content) as JsonObject ?? new JsonObject();
            }
            else
            {
                root = JsonSerializer.SerializeToNode(_configuration.CurrentValue, JsonOptions)!.AsObject();
            }
        }
        catch
        {
            root = JsonSerializer.SerializeToNode(_configuration.CurrentValue, JsonOptions)!.AsObject();
        }

        SanitizeJsonNode(root);
        return root.ToJsonString(JsonOptions);
    }

    private static void SanitizeJsonNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                var name = property.Key;
                var val = property.Value;

                if (IsSensitiveKey(name))
                {
                    obj[name] = "[REDACTED]";
                }
                else if (name.Equals("ConnectionString", StringComparison.OrdinalIgnoreCase) && val is JsonValue strVal && strVal.TryGetValue<string>(out var connStr) && connStr != null)
                {
                    obj[name] = ConnectionStringSecretRegex().Replace(connStr, "$1=[REDACTED]");
                }
                else
                {
                    SanitizeJsonNode(val);
                }
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                SanitizeJsonNode(item);
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (string.Equals(key, "JwtSecret", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Password", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Secret", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Token", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "ApiKey", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "KeyHash", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "TokenHash", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (key.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            key.Contains("Password", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string SanitizeLogLine(string line)
    {
        var sanitized = BearerTokenRegex().Replace(line, "Bearer [REDACTED]");
        sanitized = QueryOrFormSecretRegex().Replace(sanitized, "$1=[REDACTED]");
        return sanitized;
    }

    private static bool CheckDirectoryWritable(string path)
    {
        try
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            var testFile = Path.Combine(path, $".test_{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
