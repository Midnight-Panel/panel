using System.IO.Compression;
using System.Text;

namespace Midnight.ApiServer.Core.Security;

/// <summary>
/// Zip helpers that protect against zip-slip (entries escaping the target directory),
/// zip bombs (entry count and actual uncompressed bytes are limited) and duplicate entries.
/// Violations throw <see cref="InvalidDataException"/>.
/// </summary>
public static class SafeZip
{
    public static void EnsureEntryCount(ZipArchive archive, int maxEntries)
    {
        if (archive.Entries.Count > maxEntries)
            throw new InvalidDataException($"The archive contains more than {maxEntries} entries.");
    }

    /// <summary>
    /// Validates an entry name and returns the absolute target path inside <paramref name="destinationDir"/>.
    /// Absolute paths, drive letters and ".." segments are rejected.
    /// </summary>
    public static string ResolveEntryPath(string destinationDir, string entryName)
    {
        var name = entryName.Replace('\\', '/');
        if (name.Length == 0 || name.StartsWith('/') || name.Contains(':') || name.Contains('\0') ||
            name.Split('/').Any(segment => segment == ".."))
            throw new InvalidDataException($"The archive contains an invalid path '{entryName}'.");

        var root = Path.GetFullPath(destinationDir);
        if (!Path.EndsInDirectorySeparator(root))
            root += Path.DirectorySeparatorChar;

        var target = Path.GetFullPath(Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidDataException($"The archive contains an invalid path '{entryName}'.");

        return target;
    }

    public static async Task ExtractToDirectoryAsync(
        ZipArchive archive, string destinationDir, long maxTotalBytes, int maxEntries, CancellationToken cancellationToken)
    {
        EnsureEntryCount(archive, maxEntries);

        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;

        foreach (var entry in archive.Entries)
        {
            var target = ResolveEntryPath(destinationDir, entry.FullName);

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            if (!written.Add(target))
                throw new InvalidDataException($"The archive contains the duplicate entry '{entry.FullName}'.");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            await using var input = entry.Open();
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            total = await CopyLimitedAsync(input, output, total, maxTotalBytes, cancellationToken);
        }
    }

    /// <summary>Reads an entry as UTF-8 text, failing if it decompresses to more than <paramref name="maxBytes"/>.</summary>
    public static async Task<string> ReadAllTextAsync(ZipArchiveEntry entry, long maxBytes, CancellationToken cancellationToken)
    {
        if (entry.Length > maxBytes)
            throw new InvalidDataException($"'{entry.FullName}' exceeds the maximum size of {maxBytes / 1024} KB.");

        await using var input = entry.Open();
        using var buffer = new MemoryStream();
        await CopyLimitedAsync(input, buffer, 0, maxBytes, cancellationToken);
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length).TrimStart('\uFEFF');
    }

    private static async Task<long> CopyLimitedAsync(Stream input, Stream output, long total, long maxTotalBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxTotalBytes)
                throw new InvalidDataException($"The archive exceeds the maximum extracted size of {maxTotalBytes / (1024 * 1024)} MB.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return total;
    }
}
