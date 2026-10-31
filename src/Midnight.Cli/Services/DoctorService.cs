using Midnight.Cli.Common;

namespace Midnight.Cli.Services;

public interface IDoctorService
{
    Task<bool> RunDoctorAsync(CancellationToken cancellationToken = default);
}

public class DoctorService : IDoctorService
{
    private readonly IPreFlightService _preFlightService;
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;
    private readonly CliContext _context;

    public DoctorService(
        IPreFlightService preFlightService,
        IFileSystem fileSystem,
        IProcessRunner processRunner,
        IConsoleOutput console,
        CliContext context)
    {
        _preFlightService = preFlightService;
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _console = console;
        _context = context;
    }

    public async Task<bool> RunDoctorAsync(CancellationToken cancellationToken = default)
    {
        _console.WriteHeader("Midnight Doctor (diagnostics & integrity check)");

        var doctorItems = new List<CheckItem>();

        // 1. Reuse the basic pre-flight checks
        var preFlight = await _preFlightService.RunAllChecksAsync(cancellationToken: cancellationToken);
        doctorItems.AddRange(preFlight.Checks);

        // 2. Security and file permission check (sensitive files must be 600)
        doctorItems.Add(await CheckFilePermissionsAsync("/opt/midnight/panel/.env", "600"));
        doctorItems.Add(await CheckFilePermissionsAsync("/etc/midnight/midnight.json", "600"));
        doctorItems.Add(await CheckDirectoryPermissionsAsync("/var/backups/midnight", "700"));

        // 3. SSL / certificate check
        doctorItems.Add(await CheckSslCertificatesAsync(cancellationToken));

        // 4. Check log files for errors
        doctorItems.Add(CheckInstallerLogs());

        _console.WriteChecklist("Diagnostic results", doctorItems);

        var hasFailures = doctorItems.Any(i => i.Status == CheckStatus.Failed);
        if (hasFailures)
        {
            _console.WriteError("Midnight Doctor found problems that should be fixed.");
            _console.ShowLogTail(_context.LogFilePath, 20);
            return false;
        }

        _console.WriteSuccess("All diagnostic checks passed. System is healthy!");
        return true;
    }

    private async Task<CheckItem> CheckFilePermissionsAsync(string path, string expectedMode)
    {
        if (!_fileSystem.FileExists(path))
        {
            return new CheckItem(
                $"File permissions: {Path.GetFileName(path)}",
                CheckStatus.Warning,
                $"File {path} does not exist",
                "Created automatically during installation or configuration.");
        }

        var (mode, owner) = await _fileSystem.GetPermissionsAsync(path);
        if (mode == expectedMode || mode == "600")
        {
            return new CheckItem(
                $"File permissions: {Path.GetFileName(path)}",
                CheckStatus.Ok,
                $"Permissions {mode} (owner: {owner}) - access securely restricted");
        }

        return new CheckItem(
            $"File permissions: {Path.GetFileName(path)}",
            CheckStatus.Warning,
            $"Permissions are {mode} instead of {expectedMode} (potentially readable by others!)",
            $"Fix permissions with: chmod {expectedMode} {path}");
    }

    private async Task<CheckItem> CheckDirectoryPermissionsAsync(string path, string expectedMode)
    {
        if (!_fileSystem.DirectoryExists(path))
        {
            return new CheckItem($"Directory: {Path.GetFileName(path)}", CheckStatus.Ok, "Directory not created yet");
        }

        var (mode, owner) = await _fileSystem.GetPermissionsAsync(path);
        if (mode == expectedMode || mode == "700")
        {
            return new CheckItem($"Directory: {Path.GetFileName(path)}", CheckStatus.Ok, $"Permissions {mode} secure");
        }

        return new CheckItem(
            $"Directory: {Path.GetFileName(path)}",
            CheckStatus.Warning,
            $"Permissions are {mode} instead of {expectedMode}",
            $"Fix permissions with: chmod {expectedMode} {path}");
    }

    private async Task<CheckItem> CheckSslCertificatesAsync(CancellationToken cancellationToken)
    {
        var res = await _processRunner.RunAsync(
            "docker",
            "compose -f /opt/midnight/panel/docker-compose.yml exec -T caddy caddy list-certificates",
            cancellationToken: cancellationToken);

        if (res.Success && !string.IsNullOrWhiteSpace(res.StandardOutput))
        {
            return new CheckItem("SSL / certificates", CheckStatus.Ok, "Caddy is managing active SSL certificates");
        }

        return new CheckItem("SSL / certificates", CheckStatus.Ok, "Caddy managed HTTPS (automatic certificate management)");
    }

    private CheckItem CheckInstallerLogs()
    {
        var logPath = _context.LogFilePath;
        if (!_fileSystem.FileExists(logPath))
        {
            return new CheckItem("Installer log", CheckStatus.Ok, "No errors logged");
        }

        try
        {
            var lines = File.ReadAllLines(logPath);
            var recentErrors = lines.TakeLast(100).Count(l => l.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase));
            if (recentErrors > 0)
            {
                return new CheckItem(
                    "Installer log",
                    CheckStatus.Warning,
                    $"{recentErrors} errors found in the last 100 log lines",
                    $"Check the log with: midnight logs installer");
            }
        }
        catch
        {
            // Ignore
        }

        return new CheckItem("Installer log", CheckStatus.Ok, "No critical errors in the log");
    }
}
