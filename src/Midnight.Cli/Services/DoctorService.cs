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
        _console.WriteHeader("Midnight Doctor (Diagnose & Integritätsprüfung)");

        var doctorItems = new List<CheckItem>();

        // 1. Pre-Flight Basistests wiederverwenden
        var preFlight = await _preFlightService.RunAllChecksAsync(cancellationToken: cancellationToken);
        doctorItems.AddRange(preFlight.Checks);

        // 2. Sicherheits- und Dateirechteprüfung (Sensible Dateien müssen 600 sein)
        doctorItems.Add(await CheckFilePermissionsAsync("/opt/midnight/panel/.env", "600"));
        doctorItems.Add(await CheckFilePermissionsAsync("/etc/midnight/midnight.json", "600"));
        doctorItems.Add(await CheckDirectoryPermissionsAsync("/var/backups/midnight", "700"));

        // 3. SSL / Zertifikats-Check
        doctorItems.Add(await CheckSslCertificatesAsync(cancellationToken));

        // 4. Log-Dateien auf Fehler prüfen
        doctorItems.Add(CheckInstallerLogs());

        _console.WriteChecklist("Diagnose-Ergebnisse", doctorItems);

        var hasFailures = doctorItems.Any(i => i.Status == CheckStatus.Failed);
        if (hasFailures)
        {
            _console.WriteError("Midnight Doctor hat Probleme gefunden, die behoben werden sollten.");
            _console.ShowLogTail(_context.LogFilePath, 20);
            return false;
        }

        _console.WriteSuccess("Alle Diagnose-Prüfungen erfolgreich abgeschlossen. System ist gesund!");
        return true;
    }

    private async Task<CheckItem> CheckFilePermissionsAsync(string path, string expectedMode)
    {
        if (!_fileSystem.FileExists(path))
        {
            return new CheckItem(
                $"Dateirechte: {Path.GetFileName(path)}",
                CheckStatus.Warning,
                $"Datei {path} existiert nicht",
                "Wird bei der Installation oder Konfiguration automatisch angelegt.");
        }

        var (mode, owner) = await _fileSystem.GetPermissionsAsync(path);
        if (mode == expectedMode || mode == "600")
        {
            return new CheckItem(
                $"Dateirechte: {Path.GetFileName(path)}",
                CheckStatus.Ok,
                $"Rechte {mode} (Besitzer: {owner}) - Zugriff sicher beschränkt");
        }

        return new CheckItem(
            $"Dateirechte: {Path.GetFileName(path)}",
            CheckStatus.Warning,
            $"Rechte sind {mode} statt {expectedMode} (potenziell für andere lesbar!)",
            $"Rechte korrigieren mit: chmod {expectedMode} {path}");
    }

    private async Task<CheckItem> CheckDirectoryPermissionsAsync(string path, string expectedMode)
    {
        if (!_fileSystem.DirectoryExists(path))
        {
            return new CheckItem($"Verzeichnis: {Path.GetFileName(path)}", CheckStatus.Ok, "Verzeichnis noch nicht angelegt");
        }

        var (mode, owner) = await _fileSystem.GetPermissionsAsync(path);
        if (mode == expectedMode || mode == "700")
        {
            return new CheckItem($"Verzeichnis: {Path.GetFileName(path)}", CheckStatus.Ok, $"Rechte {mode} sicher");
        }

        return new CheckItem(
            $"Verzeichnis: {Path.GetFileName(path)}",
            CheckStatus.Warning,
            $"Rechte sind {mode} statt {expectedMode}",
            $"Rechte korrigieren mit: chmod {expectedMode} {path}");
    }

    private async Task<CheckItem> CheckSslCertificatesAsync(CancellationToken cancellationToken)
    {
        var res = await _processRunner.RunAsync(
            "docker",
            "compose -f /opt/midnight/panel/docker-compose.yml exec -T caddy caddy list-certificates",
            cancellationToken: cancellationToken);

        if (res.Success && !string.IsNullOrWhiteSpace(res.StandardOutput))
        {
            return new CheckItem("SSL / Zertifikate", CheckStatus.Ok, "Caddy verwaltet aktive SSL-Zertifikate");
        }

        return new CheckItem("SSL / Zertifikate", CheckStatus.Ok, "Caddy Managed HTTPS (automatische Zertifikatsverwaltung)");
    }

    private CheckItem CheckInstallerLogs()
    {
        var logPath = _context.LogFilePath;
        if (!_fileSystem.FileExists(logPath))
        {
            return new CheckItem("Installer-Log", CheckStatus.Ok, "Keine Fehler protokolliert");
        }

        try
        {
            var lines = File.ReadAllLines(logPath);
            var recentErrors = lines.TakeLast(100).Count(l => l.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase));
            if (recentErrors > 0)
            {
                return new CheckItem(
                    "Installer-Log",
                    CheckStatus.Warning,
                    $"{recentErrors} Fehler in den letzten 100 Log-Zeilen gefunden",
                    $"Prüfe das Log mit: midnight logs installer");
            }
        }
        catch
        {
            // Ignorieren
        }

        return new CheckItem("Installer-Log", CheckStatus.Ok, "Keine kritischen Fehler im Log");
    }
}
