using Midnight.Cli.Common;

namespace Midnight.Cli.Tests.Mocks;

public class MockFileSystem : IFileSystem
{
    public Dictionary<string, string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public long FreeDiskSpaceBytes { get; set; } = 50L * 1024 * 1024 * 1024; // 50 GB default
    public string MockSha256 { get; set; } = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    public bool FileExists(string path) => Files.ContainsKey(path);

    public bool DirectoryExists(string path) => Directories.Contains(path);

    public Task<string> ReadAllTextAsync(string path)
    {
        if (Files.TryGetValue(path, out var content))
            return Task.FromResult(content);
        throw new FileNotFoundException("Mock file not found", path);
    }

    public Task WriteAllTextAsync(string path, string content, bool isSecret = false)
    {
        Files[path] = content;
        if (isSecret)
        {
            Permissions[path] = "600";
        }
        return Task.CompletedTask;
    }

    public void CreateDirectory(string path) => Directories.Add(path);

    public void DeleteFile(string path) => Files.Remove(path);

    public void DeleteDirectory(string path, bool recursive = true) => Directories.Remove(path);

    public IEnumerable<string> GetFiles(string path, string searchPattern)
    {
        return Files.Keys.Where(k => k.StartsWith(path, StringComparison.OrdinalIgnoreCase));
    }

    public Task SetPermissionsAsync(string path, string octalMode = "600")
    {
        Permissions[path] = octalMode;
        return Task.CompletedTask;
    }

    public Task<(string Mode, string Owner)> GetPermissionsAsync(string path)
    {
        var mode = Permissions.TryGetValue(path, out var m) ? m : "644";
        return Task.FromResult((mode, "root"));
    }

    public long GetFreeDiskSpaceBytes(string path) => FreeDiskSpaceBytes;

    public Task<string> ComputeSha256Async(string filePath) => Task.FromResult(MockSha256);

    public Task CreateTarGzAsync(string archivePath, IDictionary<string, string> filesToInclude)
    {
        Files[archivePath] = "[MOCK-TAR-GZ-CONTENT]";
        return Task.CompletedTask;
    }

    public Task ExtractTarGzAsync(string archivePath, string destinationDir)
    {
        Directories.Add(destinationDir);
        Files[$"{destinationDir}/manifest.json"] = "{\"Version\":\"1.0\",\"PanelVersion\":\"v0.1.0\"}";
        return Task.CompletedTask;
    }
}
