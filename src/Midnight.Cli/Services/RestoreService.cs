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
            _console.WriteError($"Backup-Archiv wurde nicht gefunden: {archivePath}");
            return false;
        }

        _console.WriteHeader("Midnight Restore");

        // 1. Prüfsumme prüfen (falls .sha256 vorhanden)
        var sha256File = $"{archivePath}.sha256";
        if (_fileSystem.FileExists(sha256File))
        {
            _console.WriteInfo("Validiere SHA-256 Prüfsumme...");
            var expectedLine = (await _fileSystem.ReadAllTextAsync(sha256File)).Trim();
            var expectedHash = expectedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            var actualHash = await _fileSystem.ComputeSha256Async(archivePath);

            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                _console.WriteError($"Prüfsummen-Fehler! Erwartet: {expectedHash}, Berechnet: {actualHash}");
                return false;
            }
            _console.WriteSuccess("SHA-256 Prüfsumme stimmt überein.");
        }

        var tempWorkDir = Path.Combine(Path.GetTempPath(), $"midnight-restore-{Guid.NewGuid():N}");
        _fileSystem.CreateDirectory(tempWorkDir);

        try
        {
            var workingTarGz = archivePath;

            // 2. Entschlüsseln, falls .enc
            if (archivePath.EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
            {
                _console.WriteInfo("Verschlüsseltes Backup erkannt. Entschlüssele...");
                var secretKey = passphrase ?? Environment.GetEnvironmentVariable("MIDNIGHT_BACKUP_PASSPHRASE");
                if (string.IsNullOrEmpty(secretKey))
                {
                    secretKey = _console.Ask("Passphrase für die Entschlüsselung eingeben", isPassword: true);
                }

                if (string.IsNullOrEmpty(secretKey))
                {
                    _console.WriteError("Keine Passphrase angegeben. Restore abgebrochen.");
                    return false;
                }

                workingTarGz = Path.Combine(tempWorkDir, "decrypted.tar.gz");
                await DecryptFileAsync(archivePath, workingTarGz, secretKey);
                _console.WriteSuccess("Archiv erfolgreich entschlüsselt.");
            }

            // 3. Archiv in temporäres Verzeichnis entpacken
            _console.WriteInfo("Entpacke Backup-Dateien...");
            var extractDir = Path.Combine(tempWorkDir, "extracted");
            _fileSystem.CreateDirectory(extractDir);
            await _fileSystem.ExtractTarGzAsync(workingTarGz, extractDir);

            // 4. Manifest prüfen & Zusammenfassung anzeigen
            var manifestPath = Path.Combine(extractDir, "manifest.json");
            BackupManifest? manifest = null;
            if (_fileSystem.FileExists(manifestPath))
            {
                var json = await _fileSystem.ReadAllTextAsync(manifestPath);
                manifest = JsonSerializer.Deserialize<BackupManifest>(json);
            }

            _console.WriteLine();
            _console.WriteLine("═══ WIEDERHERSTELLUNGS-ZUSAMMENFASSUNG ═══");
            _console.WriteLine($"Archiv:          {Path.GetFileName(archivePath)}");
            _console.WriteLine($"Erstellt am:     {manifest?.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "unbekannt"}");
            _console.WriteLine($"Panel-Version:   {manifest?.PanelVersion ?? "unbekannt"}");
            _console.WriteLine($"Datenbank:       {manifest?.DatabaseProvider ?? "PostgreSQL"}");
            _console.WriteLine("Zielpfade:       /opt/midnight/panel (.env, storage, db.sql -> Postgres)");
            _console.WriteLine("══════════════════════════════════════════");
            _console.WriteLine();

            // 5. Bestätigung anfordern (außer -y)
            var confirmed = _console.Confirm(
                "Möchtest du das Panel und die Datenbank wirklich aus diesem Backup wiederherstellen?",
                defaultYes: false);

            if (!confirmed)
            {
                _console.WriteWarning("Wiederherstellung durch Benutzer abgebrochen.");
                return false;
            }

            // 6. Automatische Sicherheits-Sicherung vor dem Überschreiben
            _console.WriteInfo("Erstelle automatisches Sicherheits-Backup des aktuellen Zustands...");
            await _backupService.CreateBackupAsync(prefix: "midnight-backup-pre-restore", cancellationToken: cancellationToken);
            _console.WriteSuccess("Sicherheits-Backup gespeichert.");

            // 7. Stack stoppen
            _console.WriteInfo("Stoppe aktuellen Docker-Compose-Stack...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml down", isMutating: true, cancellationToken: cancellationToken);

            // 8. Datenbank-Container isoliert starten für Restore
            _console.WriteInfo("Starte Datenbank für Datenwiederherstellung...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml up -d db", isMutating: true, cancellationToken: cancellationToken);

            // Warten bis PostgreSQL bereit ist
            await Task.Delay(3000, cancellationToken);

            // 9. SQL-Dump einspielen
            var dbDump = Path.Combine(extractDir, "db.sql");
            if (_fileSystem.FileExists(dbDump))
            {
                _console.WriteInfo("Spiele PostgreSQL Datenbank-Dump ein...");
                var restoreScript = $"docker compose -f /opt/midnight/panel/docker-compose.yml exec -T db psql -U midnight -d midnight < \"{dbDump}\"";
                var dbRes = await _processRunner.RunBashAsync(restoreScript, isMutating: true, cancellationToken: cancellationToken);
                if (!dbRes.Success && !_context.DryRun)
                {
                    _console.WriteWarning($"Warnung beim DB-Import: {dbRes.Output}");
                }
                else
                {
                    _console.WriteSuccess("Datenbank erfolgreich wiederhergestellt.");
                }
            }

            // 10. .env und Konfigurationen wiederherstellen
            var extractedEnv = Path.Combine(extractDir, ".env");
            if (_fileSystem.FileExists(extractedEnv))
            {
                var targetEnv = "/opt/midnight/panel/.env";
                var envContent = await _fileSystem.ReadAllTextAsync(extractedEnv);
                await _fileSystem.WriteAllTextAsync(targetEnv, envContent, isSecret: true);
                await _fileSystem.SetPermissionsAsync(targetEnv, "600");
                _console.WriteSuccess(".env-Datei wiederhergestellt (Rechte 600 gesetzt).");
            }

            var extractedState = Path.Combine(extractDir, "midnight.json");
            if (_fileSystem.FileExists(extractedState))
            {
                var stateContent = await _fileSystem.ReadAllTextAsync(extractedState);
                await _fileSystem.WriteAllTextAsync(_stateService.StateFilePath, stateContent, isSecret: true);
                await _fileSystem.SetPermissionsAsync(_stateService.StateFilePath, "600");
            }

            // 11. Storage-Verzeichnis synchronisieren
            var extractedStorage = Path.Combine(extractDir, "storage");
            if (_fileSystem.DirectoryExists(extractedStorage))
            {
                var targetStorage = "/opt/midnight/panel/storage";
                _fileSystem.CreateDirectory(targetStorage);
                await _processRunner.RunAsync("cp", $"-rT \"{extractedStorage}\" \"{targetStorage}\"", isMutating: true, cancellationToken: cancellationToken);
                _console.WriteSuccess("Storage-Dateien wiederhergestellt.");
            }

            // 12. Gesamten Stack neu starten
            _console.WriteInfo("Starte vollständigen Midnight-Stack neu...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml up -d", isMutating: true, cancellationToken: cancellationToken);

            // 13. Health-Check durchführen
            _console.WriteInfo("Führe Health-Check des wiederhergestellten Panels aus...");
            var healthy = await WaitForHealthCheckAsync(cancellationToken);

            if (healthy)
            {
                _console.WriteSuccess("Wiederherstellung erfolgreich abgeschlossen! Panel ist online und gesund.");
                return true;
            }

            _console.WriteError("Health-Check nach Wiederherstellung fehlgeschlagen!");
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
            if (res.Success && res.StandardOutput.Contains("healthy", StringComparison.OrdinalIgnoreCase))
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

        using var kdf = new Rfc2898DeriveBytes(passphrase, salt, 100_000, HashAlgorithmName.SHA256);
        var key = kdf.GetBytes(32);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        await using var outputStream = File.Create(outputFilePath);
        await using var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        await cryptoStream.CopyToAsync(outputStream);
    }
}
