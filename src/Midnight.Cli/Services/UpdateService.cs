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

        // 1. Create an automatic backup before the update
        _console.WriteInfo("Creating automatic safety backup before the update...");
        string preUpdateBackupPath;
        try
        {
            preUpdateBackupPath = await _backupService.CreateBackupAsync(
                prefix: "midnight-backup-pre-update",
                cancellationToken: cancellationToken);
            _console.WriteSuccess($"Safety backup saved: {preUpdateBackupPath}");
        }
        catch (Exception ex)
        {
            _console.WriteError($"Failed to create pre-update backup: {ex.Message}");
            return false;
        }

        // Save previous version in state
        state.PreviousPanelVersion = currentVersion;
        state.LastBackupPath = preUpdateBackupPath;
        state.UseImages = shouldUseImages;
        await _stateService.SaveAsync(state);

        var updateSuccess = false;

        // 2. Method: either Docker registry images or source build
        if (shouldUseImages)
        {
            _console.WriteInfo($"[Images mode] Pulling prebuilt Docker image from the registry for {version}...");
            var imageTag = version == "latest" ? "latest" : version.TrimStart('v');
            var imageName = $"ghcr.io/midnight-panel/panel:{imageTag}";

            var pullRes = await _processRunner.RunAsync("docker", $"pull {imageName}", cancellationToken: cancellationToken);
            if (pullRes.Success)
            {
                _console.WriteSuccess($"Image {imageName} downloaded successfully.");
                // Adjust docker-compose.yml and update the stack
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
                _console.WriteWarning($"Could not pull Docker image {imageName}. Switching to source build automatically...");
                updateSuccess = await BuildFromSourceAsync(version, cancellationToken);
            }
        }
        else
        {
            updateSuccess = await BuildFromSourceAsync(version, cancellationToken);
        }

        if (!updateSuccess)
        {
            _console.WriteError("Update process could not be completed. Performing automatic rollback...");
            await TriggerAutoRollbackAsync(preUpdateBackupPath, currentVersion, cancellationToken);
            return false;
        }

        // 3. Post-update health check
        _console.WriteInfo("Running health check on the updated panel...");
        var isHealthy = await WaitForHealthCheckAsync(cancellationToken);

        if (isHealthy)
        {
            state.PanelVersion = version;
            await _stateService.SaveAsync(state);
            _console.WriteSuccess($"Update to Midnight Panel {version} completed successfully!");
            return true;
        }

        // 4. Automatic rollback on failed health check
        _console.WriteError("Health check failed after the update! The new panel is not responding correctly.");
        _console.WriteWarning("Initiating AUTOMATIC ROLLBACK to the previous version...");

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
            _console.WriteError($"Daemon source directory '{srcDir}' not found.");
            return false;
        }

        _console.WriteInfo("Updating daemon repository...");
        await _processRunner.RunAsync("git", "fetch --tags", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        if (version != "latest")
        {
            await _processRunner.RunAsync("git", $"checkout {version}", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        }
        else
        {
            await _processRunner.RunAsync("git", "pull origin main", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        }

        _console.WriteInfo("Recompiling daemon binary...");
        var buildRes = await _processRunner.RunAsync("dotnet", "publish -c Release -o /opt/midnight/daemon", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        if (!buildRes.Success && !_context.DryRun)
        {
            _console.WriteError($"Failed to compile the daemon: {buildRes.Output}");
            return false;
        }

        _console.WriteInfo("Restarting midnight-daemon service...");
        await _processRunner.RunAsync("systemctl", "restart midnight-daemon", isMutating: true, cancellationToken: cancellationToken);

        state.PreviousDaemonVersion = state.DaemonVersion;
        state.DaemonVersion = version;
        await _stateService.SaveAsync(state);

        _console.WriteSuccess($"Midnight Daemon successfully updated to {version}!");
        return true;
    }

    public async Task<bool> RollbackPanelAsync(CancellationToken cancellationToken = default)
    {
        var state = await _stateService.LoadAsync();
        if (string.IsNullOrEmpty(state.PreviousPanelVersion) && string.IsNullOrEmpty(state.LastBackupPath))
        {
            _console.WriteError("No previous version or backup for a rollback is recorded in the state.");
            return false;
        }

        var prevVersion = state.PreviousPanelVersion ?? "previous version";
        _console.WriteHeader($"Manual rollback to {prevVersion}");

        if (!string.IsNullOrEmpty(state.LastBackupPath) && _fileSystem.FileExists(state.LastBackupPath))
        {
            _console.WriteInfo($"Restoring state from last pre-update backup '{Path.GetFileName(state.LastBackupPath)}'...");
            var restored = await _restoreService.RestoreAsync(state.LastBackupPath, cancellationToken: cancellationToken);
            if (restored)
            {
                state.PanelVersion = prevVersion;
                state.PreviousPanelVersion = null;
                await _stateService.SaveAsync(state);
                _console.WriteSuccess($"Rollback to version {prevVersion} completed successfully!");
                return true;
            }
            return false;
        }

        // If no backup archive is left, try a source code rollback
        _console.WriteInfo($"Resetting source code to {prevVersion}...");
        var srcDir = "/opt/midnight/src/panel";
        await _processRunner.RunAsync("git", $"checkout {prevVersion}", workingDirectory: srcDir, isMutating: true, cancellationToken: cancellationToken);
        await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml up -d --build", isMutating: true, cancellationToken: cancellationToken);

        state.PanelVersion = prevVersion;
        state.PreviousPanelVersion = null;
        await _stateService.SaveAsync(state);
        _console.WriteSuccess($"Rollback to {prevVersion} completed.");
        return true;
    }

    public async Task<bool> RollbackDaemonAsync(CancellationToken cancellationToken = default)
    {
        var state = await _stateService.LoadAsync();
        if (string.IsNullOrEmpty(state.PreviousDaemonVersion))
        {
            _console.WriteError("No previous daemon version registered for rollback.");
            return false;
        }

        return await UpdateDaemonAsync(state.PreviousDaemonVersion, cancellationToken);
    }

    private async Task<bool> BuildFromSourceAsync(string version, CancellationToken cancellationToken)
    {
        var srcDir = "/opt/midnight/src/panel";
        _console.WriteInfo($"[Source build] Updating source code in {srcDir}...");

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

        _console.WriteInfo("Rebuilding and restarting Docker Compose containers...");
        var buildRes = await _processRunner.RunAsync(
            "docker",
            "compose -f /opt/midnight/panel/docker-compose.yml up -d --build",
            isMutating: true,
            cancellationToken: cancellationToken);

        return buildRes.Success;
    }

    private async Task TriggerAutoRollbackAsync(string backupPath, string previousVersion, CancellationToken cancellationToken)
    {
        _console.WriteWarning("=== PERFORMING AUTOMATIC ROLLBACK ===");
        if (_fileSystem.FileExists(backupPath))
        {
            await _restoreService.RestoreAsync(backupPath, cancellationToken: cancellationToken);
        }

        var state = await _stateService.LoadAsync();
        state.PanelVersion = previousVersion;
        await _stateService.SaveAsync(state);

        _console.WriteWarning($"System was successfully reverted to the previous state ({previousVersion}).");
    }

    private async Task<bool> WaitForHealthCheckAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(2000, cancellationToken);
            var res = await _processRunner.RunAsync("curl", "-fsS http://localhost:8080/api/health", cancellationToken: cancellationToken);
            if (res.Success)
            {
                return true;
            }
        }
        return false;
    }
}
