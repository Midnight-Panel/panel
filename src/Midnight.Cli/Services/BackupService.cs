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

        try
        {
            _console.WriteInfo($"Erstelle Backup im Verzeichnis '{dir}'...");

            // 1. PostgreSQL Datenbank-Dump erstellen
            var dbDumpFile = Path.Combine(tempWorkDir, "db.sql");
            var dumpScript = "docker compose -f /opt/midnight/panel/docker-compose.yml exec -T db pg_dump -U midnight midnight";
            var dumpRes = await _processRunner.RunBashAsync($"{dumpScript} > \"{dbDumpFile}\"", isMutating: true, cancellationToken: cancellationToken);

            if (!dumpRes.Success && !_context.DryRun)
            {
                _console.WriteWarning("Konnte Live-Datenbankdump nicht direkt ausführen. Prüfe ob DB-Container läuft.");
            }

            // 2. Manifest anlegen
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

            // 3. Zu archivierende Dateien zusammenstellen
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

            // 4. Tar.Gz Archiv packen
            var tarGzPath = Path.Combine(dir, $"{baseName}.tar.gz");
            await _fileSystem.CreateTarGzAsync(tarGzPath, filesToInclude);

            // 5. Dateirechte 600 setzen (nur root darf lesen/schreiben)
            await _fileSystem.SetPermissionsAsync(tarGzPath, "600");

            var finalArchive = tarGzPath;

            // 6. Optionale Verschlüsselung (--encrypt)
            if (encrypt)
            {
                var secretKey = passphrase ?? Environment.GetEnvironmentVariable("MIDNIGHT_BACKUP_PASSPHRASE");
                if (string.IsNullOrEmpty(secretKey))
                {
                    secretKey = _console.Ask("Passphrase für Backup-Verschlüsselung eingeben", isPassword: true);
                }

                if (string.IsNullOrEmpty(secretKey))
                {
                    throw new InvalidOperationException("Verschlüsselung angefordert, aber keine Passphrase angegeben.");
                }

                var encPath = Path.Combine(dir, $"{baseName}.tar.gz.enc");
                await EncryptFileAsync(tarGzPath, encPath, secretKey);
                _fileSystem.DeleteFile(tarGzPath);
                await _fileSystem.SetPermissionsAsync(encPath, "600");
                finalArchive = encPath;
                _console.WriteSuccess("Backup mit AES-256 verschlüsselt.");
            }

            // 7. SHA-256 Prüfsumme generieren
            if (!_context.DryRun && _fileSystem.FileExists(finalArchive))
            {
                var checksum = await _fileSystem.ComputeSha256Async(finalArchive);
                var checksumFile = $"{finalArchive}.sha256";
                var checksumContent = $"{checksum}  {Path.GetFileName(finalArchive)}{Environment.NewLine}";
                await _fileSystem.WriteAllTextAsync(checksumFile, checksumContent);
                await _fileSystem.SetPermissionsAsync(checksumFile, "600");
                _console.WriteInfo($"SHA-256 Prüfsumme: {checksum}");
            }

            // 8. Aufbewahrungsrichtlinie anwenden (--keep N)
            if (keepCount.HasValue && keepCount.Value > 0)
            {
                await CleanOldBackupsAsync(dir, keepCount.Value);
            }

            // 9. Zuletzt erstelltes Backup im Zustand merken
            state.LastBackupPath = finalArchive;
            await _stateService.SaveAsync(state);

            _console.WriteSuccess($"Backup erfolgreich erstellt: {finalArchive}");
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
            .OrderByDescending(f => f) // Timestamp-basiertes Sortieren
            .ToList();

        if (allBackups.Count > keepCount)
        {
            var toDelete = allBackups.Skip(keepCount).ToList();
            _console.WriteInfo($"Aufbewahrungsrichtlinie (--keep {keepCount}): Lösche {toDelete.Count} ältere(s) Backup(s)...");

            foreach (var oldBackup in toDelete)
            {
                _fileSystem.DeleteFile(oldBackup);
                var shaFile = $"{oldBackup}.sha256";
                if (_fileSystem.FileExists(shaFile))
                {
                    _fileSystem.DeleteFile(shaFile);
                }
                _console.WriteInfo($"  Gelöscht: {Path.GetFileName(oldBackup)}");
            }
        }

        return Task.CompletedTask;
    }

    public static async Task EncryptFileAsync(string inputFilePath, string outputFilePath, string passphrase)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var kdf = new Rfc2898DeriveBytes(passphrase, salt, 100_000, HashAlgorithmName.SHA256);
        var key = kdf.GetBytes(32); // AES-256
        var iv = RandomNumberGenerator.GetBytes(16);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        await using var outputStream = File.Create(outputFilePath);
        // Salt und IV voranstellen
        await outputStream.WriteAsync(salt);
        await outputStream.WriteAsync(iv);

        await using var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await using var inputStream = File.OpenRead(inputFilePath);
        await inputStream.CopyToAsync(cryptoStream);
    }
}
