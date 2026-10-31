using System.Security.Cryptography;
using System.Text.Json;
using Midnight.Cli.Common;
using Midnight.Cli.Models;

namespace Midnight.Cli.Services;

public interface IRestoreService
{
    Task<bool> RestoreAsync(
        string archivePath,
        string? passphrase = null,
        CancellationToken cancellationToken = default);
}

public class RestoreService : IRestoreService
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;
    private readonly IBackupService _backupService;
    private readonly IStateService _stateService;
    private readonly IConsoleOutput _console;
    private readonly CliContext _context;

    public RestoreService(
        IProcessRunner processRunner,
        IFileSystem fileSystem,
        IBackupService backupService,
        IStateService stateService,
        IConsoleOutput console,
        CliContext context)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _backupService = backupService;
        _stateService = stateService;
        _console = console;
        _context = context;
    }

    public async Task<bool> RestoreAsync(
        string archivePath,
        string? passphrase = null,
        CancellationToken cancellationToken = default)
    {
        if (!_fileSystem.FileExists(archivePath))
        {
            _console.WriteError($"Backup archive not found: {archivePath}");
            return false;
        }

        _console.WriteHeader("Midnight Restore");

        // 1. Verify checksum (if .sha256 exists)
        var sha256File = $"{archivePath}.sha256";
        if (_fileSystem.FileExists(sha256File))
        {
            _console.WriteInfo("Validating SHA-256 checksum...");
            var expectedLine = (await _fileSystem.ReadAllTextAsync(sha256File)).Trim();
            var expectedHash = expectedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var actualHash = await _fileSystem.ComputeSha256Async(archivePath);

            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                _console.WriteError($"Checksum mismatch! Expected: {expectedHash}, computed: {actualHash}");
                return false;
            }
            _console.WriteSuccess("SHA-256 checksum matches.");
        }

        var tempWorkDir = Path.Combine(Path.GetTempPath(), $"midnight-restore-{Guid.NewGuid():N}");
        _fileSystem.CreateDirectory(tempWorkDir);

        // Holds the decrypted archive, .env and database dump while restoring
        await _fileSystem.SetPermissionsAsync(tempWorkDir, "700");

        try
        {
            var workingTarGz = archivePath;

            // 2. Decrypt if .enc
            if (archivePath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
            {
                _console.WriteInfo("Encrypted backup detected. Decrypting...");
                var secretKey = passphrase ?? Environment.GetEnvironmentVariable("MIDNIGHT_BACKUP_PASSPHRASE");
                if (string.IsNullOrEmpty(secretKey))
                {
                    secretKey = _console.Ask("Enter passphrase for decryption", isPassword: true);
                }

                if (string.IsNullOrEmpty(secretKey))
                {
                    _console.WriteError("No passphrase provided. Restore aborted.");
                    return false;
                }

                workingTarGz = Path.Combine(tempWorkDir, "decrypted.tar.gz");
                await DecryptFileAsync(archivePath, workingTarGz, secretKey);
                _console.WriteSuccess("Archive decrypted successfully.");
            }

            // 3. Extract archive into a temporary directory
            _console.WriteInfo("Extracting backup files...");
            var extractDir = Path.Combine(tempWorkDir, "extracted");
            _fileSystem.CreateDirectory(extractDir);
            await _fileSystem.ExtractTarGzAsync(workingTarGz, extractDir);

            // 4. Check manifest & show summary
            var manifestPath = Path.Combine(extractDir, "manifest.json");
            BackupManifest? manifest = null;
            if (_fileSystem.FileExists(manifestPath))
            {
                var json = await _fileSystem.ReadAllTextAsync(manifestPath);
                manifest = JsonSerializer.Deserialize<BackupManifest>(json);
            }

            _console.WriteLine();
            _console.WriteLine("═══ RESTORE SUMMARY ═══");
            _console.WriteLine($"Archive:         {Path.GetFileName(archivePath)}");
            _console.WriteLine($"Created at:      {manifest?.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "unknown"}");
            _console.WriteLine($"Panel version:   {manifest?.PanelVersion ?? "unknown"}");
            _console.WriteLine($"Database:        {manifest?.DatabaseProvider ?? "PostgreSQL"}");
            _console.WriteLine("Target paths:    /opt/midnight/panel (.env, storage, db.sql -> Postgres)");
            _console.WriteLine("══════════════════════════════════════════");
            _console.WriteLine();

            // 5. Ask for confirmation (unless -y)
            var confirmed = _console.Confirm(
                "Do you really want to restore the panel and database from this backup?",
                defaultYes: false);

            if (!confirmed)
            {
                _console.WriteWarning("Restore aborted by user.");
                return false;
            }

            // 6. Automatic safety backup before overwriting
            _console.WriteInfo("Creating automatic safety backup of the current state...");
            await _backupService.CreateBackupAsync(prefix: "midnight-backup-pre-restore", cancellationToken: cancellationToken);
            _console.WriteSuccess("Safety backup saved.");

            // 7. Stop the stack
            _console.WriteInfo("Stopping current Docker Compose stack...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml down", isMutating: true, cancellationToken: cancellationToken);

            // 8. Start the database container on its own for the restore
            _console.WriteInfo("Starting database for data restore...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml up -d db", isMutating: true, cancellationToken: cancellationToken);

            // Wait until PostgreSQL is ready
            await Task.Delay(3000, cancellationToken);

            // 9. Import SQL dump
            var dbDump = Path.Combine(extractDir, "db.sql");
            if (_fileSystem.FileExists(dbDump))
            {
                _console.WriteInfo("Importing PostgreSQL database dump...");
                var restoreScript = $"docker compose -f /opt/midnight/panel/docker-compose.yml exec -T db psql -U midnight -d midnight < \"{dbDump}\"";
                var dbRes = await _processRunner.RunBashAsync(restoreScript, isMutating: true, cancellationToken: cancellationToken);
                if (!dbRes.Success && !_context.DryRun)
                {
                    _console.WriteWarning($"Warning during DB import: {dbRes.Output}");
                }
                else
                {
                    _console.WriteSuccess("Database restored successfully.");
                }
            }

            // 10. Restore .env and configuration
            var extractedEnv = Path.Combine(extractDir, ".env");
            if (_fileSystem.FileExists(extractedEnv))
            {
                var targetEnv = "/opt/midnight/panel/.env";
                var envContent = await _fileSystem.ReadAllTextAsync(extractedEnv);
                await _fileSystem.WriteAllTextAsync(targetEnv, envContent, isSecret: true);
                await _fileSystem.SetPermissionsAsync(targetEnv, "600");
                _console.WriteSuccess(".env file restored (permissions set to 600).");
            }

            var extractedState = Path.Combine(extractDir, "midnight.json");
            if (_fileSystem.FileExists(extractedState))
            {
                var stateContent = await _fileSystem.ReadAllTextAsync(extractedState);
                await _fileSystem.WriteAllTextAsync(_stateService.StateFilePath, stateContent, isSecret: true);
                await _fileSystem.SetPermissionsAsync(_stateService.StateFilePath, "600");
            }

            // 11. Sync storage directory
            var extractedStorage = Path.Combine(extractDir, "storage");
            if (_fileSystem.DirectoryExists(extractedStorage))
            {
                var targetStorage = "/opt/midnight/panel/storage";
                _fileSystem.CreateDirectory(targetStorage);
                await _processRunner.RunAsync("cp", $"-rT \"{extractedStorage}\" \"{targetStorage}\"", isMutating: true, cancellationToken: cancellationToken);
                _console.WriteSuccess("Storage files restored.");
            }

            // 12. Restart the whole stack
            _console.WriteInfo("Restarting the full Midnight stack...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml up -d", isMutating: true, cancellationToken: cancellationToken);

            // 13. Run health check
            _console.WriteInfo("Running health check on the restored panel...");
            var healthy = await WaitForHealthCheckAsync(cancellationToken);

            if (healthy)
            {
                _console.WriteSuccess("Restore completed successfully! Panel is online and healthy.");
                return true;
            }

            _console.WriteError("Health check failed after restore!");
            return false;
        }
        finally
        {
            _fileSystem.DeleteDirectory(tempWorkDir, recursive: true);
        }
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

    public static async Task DecryptFileAsync(string inputFilePath, string outputFilePath, string passphrase)
    {
        await using var inputStream = File.OpenRead(inputFilePath);

        var salt = new byte[16];
        await inputStream.ReadExactlyAsync(salt);

        var iv = new byte[16];
        await inputStream.ReadExactlyAsync(iv);

        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, 100_000, HashAlgorithmName.SHA256, 32);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        await using var outputStream = File.Create(outputFilePath);
        await using var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        await cryptoStream.CopyToAsync(outputStream);
    }
}
