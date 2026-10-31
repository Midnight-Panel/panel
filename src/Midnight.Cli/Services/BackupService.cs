using System.Security.Cryptography;
using System.Text.Json;
using Midnight.Cli.Common;
using Midnight.Cli.Models;

namespace Midnight.Cli.Services;

public interface IBackupService
{
    Task<string> CreateBackupAsync(
        string? targetDir = null,
        bool encrypt = false,
        string? passphrase = null,
        int? keepCount = null,
        string? prefix = null,
        CancellationToken cancellationToken = default);

    Task CleanOldBackupsAsync(string backupDir, int keepCount);
}

public class BackupService : IBackupService
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;
    private readonly IStateService _stateService;
    private readonly IConsoleOutput _console;
    private readonly CliContext _context;

    public const string DefaultBackupDirectory = "/var/backups/midnight";

    public BackupService(
        IProcessRunner processRunner,
        IFileSystem fileSystem,
        IStateService stateService,
        IConsoleOutput console,
        CliContext context)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _stateService = stateService;
        _console = console;
        _context = context;
    }

    public async Task<string> CreateBackupAsync(
        string? targetDir = null,
        bool encrypt = false,
        string? passphrase = null,
        int? keepCount = null,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var dir = string.IsNullOrWhiteSpace(targetDir) ? DefaultBackupDirectory : targetDir;
        _fileSystem.CreateDirectory(dir);

        var state = await _stateService.LoadAsync();
        var version = state.PanelVersion.Replace("/", "_");
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var backupPrefix = string.IsNullOrEmpty(prefix) ? "midnight-backup" : prefix;
        var baseName = $"{backupPrefix}-{version}-{timestamp}";

        var tempWorkDir = Path.Combine(Path.GetTempPath(), $"midnight-backup-{Guid.NewGuid():N}");
        _fileSystem.CreateDirectory(tempWorkDir);

        // The database dump is written here before it is packed, other local users must not read it
        await _fileSystem.SetPermissionsAsync(tempWorkDir, "700");

        try
        {
            _console.WriteInfo($"Creating backup in directory '{dir}'...");

            // 1. Create PostgreSQL database dump
            var dbDumpFile = Path.Combine(tempWorkDir, "db.sql");
            var dumpScript = "docker compose -f /opt/midnight/panel/docker-compose.yml exec -T db pg_dump -U midnight midnight";
            var dumpRes = await _processRunner.RunBashAsync($"{dumpScript} > \"{dbDumpFile}\"", isMutating: true, cancellationToken: cancellationToken);

            if (!dumpRes.Success && !_context.DryRun)
            {
                _console.WriteWarning("Could not run live database dump. Check whether the DB container is running.");
            }

            // 2. Create manifest
            var manifest = new BackupManifest
            {
                Version = "1.0",
                PanelVersion = state.PanelVersion,
                CreatedAt = DateTime.UtcNow,
                DatabaseProvider = "PostgreSQL",
                Encrypted = encrypt,
                EncryptionAlgorithm = encrypt ? "AES-256-CBC" : null,
                IncludedFiles = ["db.sql", ".env", "midnight.json", "storage"],
                SystemArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
            };

            var manifestPath = Path.Combine(tempWorkDir, "manifest.json");
            await _fileSystem.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

            // 3. Collect files to archive
            var filesToInclude = new Dictionary<string, string>
            {
                ["manifest.json"] = manifestPath
            };

            if (_fileSystem.FileExists(dbDumpFile))
            {
                filesToInclude["db.sql"] = dbDumpFile;
            }

            var envPath = "/opt/midnight/panel/.env";
            if (_fileSystem.FileExists(envPath))
            {
                filesToInclude[".env"] = envPath;
            }

            if (_fileSystem.FileExists(_stateService.StateFilePath))
            {
                filesToInclude["midnight.json"] = _stateService.StateFilePath;
            }

            var storagePath = "/opt/midnight/panel/storage";
            if (_fileSystem.DirectoryExists(storagePath))
            {
                filesToInclude["storage"] = storagePath;
            }

            // 4. Pack tar.gz archive
            var tarGzPath = Path.Combine(dir, $"{baseName}.tar.gz");
            await _fileSystem.CreateTarGzAsync(tarGzPath, filesToInclude);

            // 5. Set file permissions to 600 (only root may read/write)
            await _fileSystem.SetPermissionsAsync(tarGzPath, "600");

            var finalArchive = tarGzPath;

            // 6. Optional encryption (--encrypt)
            if (encrypt)
            {
                var secretKey = passphrase ?? Environment.GetEnvironmentVariable("MIDNIGHT_BACKUP_PASSPHRASE");
                if (string.IsNullOrEmpty(secretKey))
                {
                    secretKey = _console.Ask("Enter passphrase for backup encryption", isPassword: true);
                }

                if (string.IsNullOrEmpty(secretKey))
                {
                    throw new InvalidOperationException("Encryption requested, but no passphrase provided.");
                }

                var encPath = Path.Combine(dir, $"{baseName}.tar.gz.enc");
                await EncryptFileAsync(tarGzPath, encPath, secretKey);
                _fileSystem.DeleteFile(tarGzPath);
                await _fileSystem.SetPermissionsAsync(encPath, "600");
                finalArchive = encPath;
                _console.WriteSuccess("Backup encrypted with AES-256.");
            }

            // 7. Generate SHA-256 checksum
            if (!_context.DryRun && _fileSystem.FileExists(finalArchive))
            {
                var checksum = await _fileSystem.ComputeSha256Async(finalArchive);
                var checksumFile = $"{finalArchive}.sha256";
                var checksumContent = $"{checksum}  {Path.GetFileName(finalArchive)}{Environment.NewLine}";
                await _fileSystem.WriteAllTextAsync(checksumFile, checksumContent);
                await _fileSystem.SetPermissionsAsync(checksumFile, "600");
                _console.WriteInfo($"SHA-256 checksum: {checksum}");
            }

            // 8. Apply retention policy (--keep N)
            if (keepCount.HasValue && keepCount.Value > 0)
            {
                await CleanOldBackupsAsync(dir, keepCount.Value);
            }

            // 9. Record the latest backup in state
            state.LastBackupPath = finalArchive;
            await _stateService.SaveAsync(state);

            _console.WriteSuccess($"Backup created successfully: {finalArchive}");
            return finalArchive;
        }
        finally
        {
            _fileSystem.DeleteDirectory(tempWorkDir, recursive: true);
        }
    }

    public Task CleanOldBackupsAsync(string backupDir, int keepCount)
    {
        if (keepCount <= 0 || !_fileSystem.DirectoryExists(backupDir))
            return Task.CompletedTask;

        var allBackups = _fileSystem.GetFiles(backupDir, "midnight-backup-*")
            .Where(f => f.EndsWith(".tar.gz") || f.EndsWith(".tar.gz.enc"))
            .OrderByDescending(f => f) // Timestamp-based sorting
            .ToList();

        if (allBackups.Count > keepCount)
        {
            var toDelete = allBackups.Skip(keepCount).ToList();
            _console.WriteInfo($"Retention policy (--keep {keepCount}): deleting {toDelete.Count} older backup(s)...");

            foreach (var oldBackup in toDelete)
            {
                _fileSystem.DeleteFile(oldBackup);
                var shaFile = $"{oldBackup}.sha256";
                if (_fileSystem.FileExists(shaFile))
                {
                    _fileSystem.DeleteFile(shaFile);
                }
                _console.WriteInfo($"  Deleted: {Path.GetFileName(oldBackup)}");
            }
        }

        return Task.CompletedTask;
    }

    public static async Task EncryptFileAsync(string inputFilePath, string outputFilePath, string passphrase)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, 100_000, HashAlgorithmName.SHA256, 32); // AES-256
        var iv = RandomNumberGenerator.GetBytes(16);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        await using var outputStream = File.Create(outputFilePath);
        // Prepend salt and IV
        await outputStream.WriteAsync(salt);
        await outputStream.WriteAsync(iv);

        await using var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await using var inputStream = File.OpenRead(inputFilePath);
        await inputStream.CopyToAsync(cryptoStream);
    }
}
