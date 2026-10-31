using Midnight.Cli.Common;

namespace Midnight.Cli.Services;

public interface IUpdateService
{
    Task<bool> UpdatePanelAsync(
        string? targetVersion = null,
        bool? useImages = null,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateDaemonAsync(
        string? targetVersion = null,
        CancellationToken cancellationToken = default);

    Task<bool> RollbackPanelAsync(CancellationToken cancellationToken = default);
    Task<bool> RollbackDaemonAsync(CancellationToken cancellationToken = default);
}

public class UpdateService : IUpdateService
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;
    private readonly IBackupService _backupService;
    private readonly IRestoreService _restoreService;
    private readonly IStateService _stateService;
    private readonly IConsoleOutput _console;
    private readonly CliContext _context;

    public UpdateService(
        IProcessRunner processRunner,
        IFileSystem fileSystem,
        IBackupService backupService,
        IRestoreService restoreService,
        IStateService stateService,
        IConsoleOutput console,
        CliContext context)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _backupService = backupService;
        _restoreService = restoreService;
        _stateService = stateService;
        _console = console;
        _context = context;
    }

    public async Task<bool> UpdatePanelAsync(
        string? targetVersion = null,
        bool? useImages = null,
        CancellationToken cancellationToken = default)
    {
        var state = await _stateService.LoadAsync();
        var currentVersion = state.PanelVersion;
        var version = string.IsNullOrWhiteSpace(targetVersion) ? "latest" : targetVersion;
        var shouldUseImages = useImages ?? state.UseImages;

        _console.WriteHeader($"Midnight Panel Update ({currentVersion} -> {version})");

        // 1. Automatisches Backup vor dem Update anlegen
        _console.WriteInfo("Erstelle automatisches Sicherheits-Backup vor dem Update...");
        string preUpdateBackupPath;
        try
        {
            preUpdateBackupPath = await _backupService.CreateBackupAsync(
                prefix: "midnight-backup-pre-update",
                cancellationToken: cancellationToken);
            _console.WriteSuccess($"Sicherheits-Backup gespeichert: {preUpdateBackupPath}");
        }
        catch (Exception ex)
        {
            _console.WriteError($"Fehler beim Erstellen des Pre-Update-Backups: {ex.Message}");
            return false;
        }

        // Vorherige Version im Zustand sichern
        state.PreviousPanelVersion = currentVersion;
        state.LastBackupPath = preUpdateBackupPath;
        state.UseImages = shouldUseImages;
        await _stateService.SaveAsync(state);

        var updateSuccess = false;

        // 2. Methode: Entweder Docker Registry Images oder Source Build
        if (shouldUseImages)
        {
            _console.WriteInfo($"[Images-Modus] Ziehe vorgebautes Docker-Image aus der Registry für {version}...");
            var imageTag = version == "latest" ? "latest" : version.TrimStart('v');
            var imageName = $"ghcr.io/midnight-panel/panel:{imageTag}";

            var pullRes = await _processRunner.RunAsync("docker", $"pull {imageName}", cancellationToken: cancellationToken);
            if (pullRes.Success)
            {
                _console.WriteSuccess($"Image {imageName} erfolgreich heruntergeladen.");
                // docker-compose.yml anpassen und Stack aktualisieren
                var composePath = "/opt/midnight/panel/docker-compose.yml";
                if (_fileSystem.FileExists(composePath))
                {
                    var composeContent = await _fileSystem.ReadAllTextAsync(composePath);
                    var updatedCompose = System.Text.RegularExpressions.Regex.Replace(
                        composeContent,
                        @"image:\s*.*panel.*",
                        $"image: {imageName}");
                    await _fileSystem.WriteAllTextAsync(composePath, updatedCompose);
                }

                await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml up -d", isMutating: true, cancellationToken: cancellationToken);
                updateSuccess = true;
            }
            else
            {
                _console.WriteWarning($"Docker-Image {imageName} konnte nicht geladen werden. Wechsle automatisch zu Source-Build...");
                updateSuccess = await BuildFromSourceAsync(version, cancellationToken);
            }
        }
        else
        {
            updateSuccess = await BuildFromSourceAsync(version, cancellationToken);
        }

        if (!updateSuccess)
        {
            _console.WriteError("Update-Prozess konnte nicht abgeschlossen werden. Führe automatischen Rollback durch...");
            await TriggerAutoRollbackAsync(preUpdateBackupPath, currentVersion, cancellationToken);
            return false;
        }

        // 3. Post-Update Health-Check
        _console.WriteInfo("Führe Health-Check des aktualisierten Panels durch...");
        var isHealthy = await WaitForHealthCheckAsync(cancellationToken);

        if (isHealthy)
        {
            state.PanelVersion = version;
            await _stateService.SaveAsync(state);
            _console.WriteSuccess($"Update auf Midnight Panel {version} erfolgreich abgeschlossen!");
            return true;
        }

        // 4. Automatischer Rollback bei fehlgeschlagenem Health-Check
        _console.WriteError("Health-Check nach dem Update fehlgeschlagen! Das neue Panel reagiert nicht ordnungsgemäß.");
        _console.WriteWarning("Initiiere AUTOMATISCHEN ROLLBACK auf die vorherige Version...");

        await TriggerAutoRollbackAsync(preUpdateBackupPath, currentVersion, cancellationToken);
        return false;
    }

    public async Task<bool> UpdateDaemonAsync(
        string? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        var state = await _stateService.LoadAsync();
        var version = targetVersion ?? "latest";
        _console.WriteHeader($"Midnight Daemon Update ({state.DaemonVersion} -> {version})");

        var srcDir = "/opt/midnight/src/demon";
        if (!_fileSystem.DirectoryExists(srcDir))
        {
            _console.WriteError($"Daemon-Quellverzeichnis '{srcDir}' nicht gefunden.");
            return false;
        }

        _console.WriteInfo("Aktualisiere Daemon-Repository...");
        await _processRunner.RunAsync("git", "fetch --tags", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        if (version != "latest")
        {
            await _processRunner.RunAsync("git", $"checkout {version}", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        }
        else
        {
            await _processRunner.RunAsync("git", "pull origin main", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        }

        _console.WriteInfo("Kompiliere Daemon-Binary neu...");
        var buildRes = await _processRunner.RunAsync("dotnet", "publish -c Release -o /opt/midnight/daemon", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        if (!buildRes.Success && !_context.DryRun)
        {
            _console.WriteError($"Fehler beim Kompilieren des Daemons: {buildRes.Output}");
            return false;
        }

        _console.WriteInfo("Starte midnight-daemon Dienst neu...");
        await _processRunner.RunAsync("systemctl", "restart midnight-daemon", isMutating: true, cancellationToken: cancellationToken);

        state.PreviousDaemonVersion = state.DaemonVersion;
        state.DaemonVersion = version;
        await _stateService.SaveAsync(state);

        _console.WriteSuccess($"Midnight Daemon erfolgreich auf {version} aktualisiert!");
        return true;
    }

    public async Task<bool> RollbackPanelAsync(CancellationToken cancellationToken = default)
    {
        var state = await _stateService.LoadAsync();
        if (string.IsNullOrEmpty(state.PreviousPanelVersion) && string.IsNullOrEmpty(state.LastBackupPath))
        {
            _console.WriteError("Keine vorherige Version oder kein Backup für einen Rollback im Zustand hinterlegt.");
            return false;
        }

        var prevVersion = state.PreviousPanelVersion ?? "vorherige Version";
        _console.WriteHeader($"Manueller Rollback auf {prevVersion}");

        if (!string.IsNullOrEmpty(state.LastBackupPath) && _fileSystem.FileExists(state.LastBackupPath))
        {
            _console.WriteInfo($"Stelle Zustand aus letztem Pre-Update-Backup '{Path.GetFileName(state.LastBackupPath)}' wieder her...");
            var restored = await _restoreService.RestoreAsync(state.LastBackupPath, cancellationToken: cancellationToken);
            if (restored)
            {
                state.PanelVersion = prevVersion;
                state.PreviousPanelVersion = null;
                await _stateService.SaveAsync(state);
                _console.WriteSuccess($"Rollback auf Version {prevVersion} erfolgreich abgeschlossen!");
                return true;
            }
            return false;
        }

        // Falls kein Backup-Archiv mehr da, Quellcode-Rollback versuchen
        _console.WriteInfo($"Setze Quellcode auf {prevVersion} zurück...");
        var srcDir = "/opt/midnight/src/panel";
        await _processRunner.RunAsync("git", $"checkout {prevVersion}", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml up -d --build", isMutating: true, cancellationToken: cancellationToken);

        state.PanelVersion = prevVersion;
        state.PreviousPanelVersion = null;
        await _stateService.SaveAsync(state);
        _console.WriteSuccess($"Rollback auf {prevVersion} abgeschlossen.");
        return true;
    }

    public async Task<bool> RollbackDaemonAsync(CancellationToken cancellationToken = default)
    {
        var state = await _stateService.LoadAsync();
        if (string.IsNullOrEmpty(state.PreviousDaemonVersion))
        {
            _console.WriteError("Keine vorherige Daemon-Version für Rollback registriert.");
            return false;
        }

        return await UpdateDaemonAsync(state.PreviousDaemonVersion, cancellationToken);
    }

    private async Task<bool> BuildFromSourceAsync(string version, CancellationToken cancellationToken)
    {
        var srcDir = "/opt/midnight/src/panel";
        _console.WriteInfo($"[Source-Build] Aktualisiere Quellcode in {srcDir}...");

        if (_fileSystem.DirectoryExists(srcDir))
        {
            await _processRunner.RunAsync("git", "fetch --tags", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
            if (version != "latest")
            {
                await _processRunner.RunAsync("git", $"checkout {version}", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
            }
            else
            {
                await _processRunner.RunAsync("git", "pull origin main", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
            }
        }

        _console.WriteInfo("Baue und starte Docker-Compose-Container neu...");
        var buildRes = await _processRunner.RunAsync(
            "docker",
            "compose -f /opt/midnight/panel/docker-compose.yml up -d --build",
            isMutating: true,
            cancellationToken: cancellationToken);

        return buildRes.Success;
    }

    private async Task TriggerAutoRollbackAsync(string backupPath, string previousVersion, CancellationToken cancellationToken)
    {
        _console.WriteWarning("=== AUTOMATISCHER ROLLBACK WIRD AUSGEFÜHRT ===");
        if (_fileSystem.FileExists(backupPath))
        {
            await _restoreService.RestoreAsync(backupPath, cancellationToken: cancellationToken);
        }

        var state = await _stateService.LoadAsync();
        state.PanelVersion = previousVersion;
        await _stateService.SaveAsync(state);

        _console.WriteWarning($"System wurde erfolgreich auf den vorherigen Stand ({previousVersion}) zurückgesetzt.");
    }

    private async Task<bool> WaitForHealthCheckAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(2000, cancellationToken);
            var res = await _processRunner.RunAsync("curl", "-fsS http://localhost:8080/api/health", cancellationToken: cancellationToken);
            if (res.Success && res.StandardOutput.Contains("healthy", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
