namespace Midnight.Cli.Common;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string content, bool isSecret = false);
    void CreateDirectory(string path);
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive = true);
    IEnumerable<string> GetFiles(string path, string searchPattern);
    Task SetPermissionsAsync(string path, string octalMode = "600");
    Task<(string Mode, string Owner)> GetPermissionsAsync(string path);
    long GetFreeDiskSpaceBytes(string path);
    Task<string> ComputeSha256Async(string filePath);
    Task CreateTarGzAsync(string archivePath, IDictionary<string, string> filesToInclude);
    Task ExtractTarGzAsync(string archivePath, string destinationDir);
}
