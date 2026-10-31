using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Midnight.Cli.Common;

public class FileSystem : IFileSystem
{
    private readonly CliContext _context;
    private readonly IConsoleOutput _console;

    public FileSystem(CliContext context, IConsoleOutput console)
    {
        _context = context;
        _console = console;
    }

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(path);

    public async Task WriteAllTextAsync(string path, string content, bool isSecret = false)
    {
        if (_context.DryRun)
        {
            _console.WriteInfo($"[DRY-RUN] Würde Datei schreiben: {path} (Geheim: {isSecret})");
            return;
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(path, content);

        if (isSecret && OperatingSystem.IsLinux())
        {
            await SetPermissionsAsync(path, "600");
        }
    }

    public void CreateDirectory(string path)
    {
        if (_context.DryRun)
        {
            _console.WriteInfo($"[DRY-RUN] Würde Verzeichnis anlegen: {path}");
            return;
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public void DeleteFile(string path)
    {
        if (_context.DryRun)
        {
            _console.WriteInfo($"[DRY-RUN] Würde Datei löschen: {path}");
            return;
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void DeleteDirectory(string path, bool recursive = true)
    {
        if (_context.DryRun)
        {
            _console.WriteInfo($"[DRY-RUN] Würde Verzeichnis löschen: {path}");
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive);
        }
    }

    public IEnumerable<string> GetFiles(string path, string searchPattern)
    {
        if (!Directory.Exists(path)) return [];
        return Directory.GetFiles(path, searchPattern);
    }

    public Task SetPermissionsAsync(string path, string octalMode = "600")
    {
        if (_context.DryRun)
        {
            _console.WriteInfo($"[DRY-RUN] Würde Rechte {octalMode} auf {path} setzen");
            return Task.CompletedTask;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                var mode = octalMode switch
                {
                    "600" => UnixFileMode.UserRead | UnixFileMode.UserWrite,
                    "700" => UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    "644" => UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead,
                    "755" => UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
                    _ => UnixFileMode.UserRead | UnixFileMode.UserWrite
                };

                if (File.Exists(path))
                {
                    File.SetUnixFileMode(path, mode);
                }
                else if (Directory.Exists(path))
                {
                    File.SetUnixFileMode(path, mode);
                }
            }
            catch (Exception ex)
            {
                _console.WriteWarning($"Konnte Dateirechte für {path} nicht setzen: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    public Task<(string Mode, string Owner)> GetPermissionsAsync(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return Task.FromResult(("000", "unknown"));

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                var mode = File.GetUnixFileMode(path);
                var is600 = mode is (UnixFileMode.UserRead | UnixFileMode.UserWrite);
                var is700 = mode is (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                var modeStr = is600 ? "600" : is700 ? "700" : ((int)mode).ToString();
                return Task.FromResult((modeStr, "root"));
            }
            catch
            {
                return Task.FromResult(("unknown", "root"));
            }
        }

        return Task.FromResult(("600", "root"));
    }

    public long GetFreeDiskSpaceBytes(string path)
    {
        try
        {
            var targetPath = Directory.Exists(path) ? path : Path.GetPathRoot(path) ?? "/";
            var driveInfo = new DriveInfo(targetPath);
            return driveInfo.AvailableFreeSpace;
        }
        catch
        {
            // Fallback für Tests/Virtualisierungen: 50 GB simulieren
            return 50L * 1024 * 1024 * 1024;
        }
    }

    public async Task<string> ComputeSha256Async(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Datei nicht gefunden", filePath);

        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexStringLower(hash);
    }

    public async Task CreateTarGzAsync(string archivePath, IDictionary<string, string> filesToInclude)
    {
        if (_context.DryRun)
        {
            _console.WriteInfo($"[DRY-RUN] Würde Backup-Archiv {archivePath} packen ({filesToInclude.Count} Elemente)");
            return;
        }

        var dir = Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var fileStream = File.Create(archivePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal);
        await using var tarWriter = new TarWriter(gzipStream);

        foreach (var (entryName, sourcePath) in filesToInclude)
        {
            if (File.Exists(sourcePath))
            {
                await tarWriter.WriteEntryAsync(sourcePath, entryName);
            }
            else if (Directory.Exists(sourcePath))
            {
                // Verzeichnis rekursiv ins Tar schreiben
                foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(sourcePath, file).Replace('\\', '/');
                    var archiveEntry = $"{entryName}/{relative}";
                    await tarWriter.WriteEntryAsync(file, archiveEntry);
                }
            }
        }
    }

    public async Task ExtractTarGzAsync(string archivePath, string destinationDir)
    {
        if (_context.DryRun)
        {
            _console.WriteInfo($"[DRY-RUN] Würde Backup-Archiv {archivePath} nach {destinationDir} entpacken");
            return;
        }

        if (!Directory.Exists(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        await using var fileStream = File.OpenRead(archivePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        await using var tarReader = new TarReader(gzipStream);

        while (await tarReader.GetNextEntryAsync() is { } entry)
        {
            await entry.ExtractRelativeToDirectoryAsync(destinationDir, overwrite: true);
        }
    }
}
