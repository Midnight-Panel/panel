using System.Text.Json;
using Midnight.Cli.Common;
using Midnight.Cli.Models;

namespace Midnight.Cli.Services;

public interface IStateService
{
    string StateFilePath { get; }
    Task<InstallationState> LoadAsync();
    Task SaveAsync(InstallationState state);
    bool Exists();
}

public class StateService : IStateService
{
    private readonly IFileSystem _fileSystem;
    private readonly CliContext _context;

    public string StateFilePath => "/etc/midnight/midnight.json";

    public StateService(IFileSystem fileSystem, CliContext context)
    {
        _fileSystem = fileSystem;
        _context = context;
    }

    public bool Exists() => _fileSystem.FileExists(StateFilePath);

    public async Task<InstallationState> LoadAsync()
    {
        if (!Exists())
        {
            return new InstallationState();
        }

        try
        {
            var json = await _fileSystem.ReadAllTextAsync(StateFilePath);
            return JsonSerializer.Deserialize<InstallationState>(json, JsonOptions) ?? new InstallationState();
        }
        catch
        {
            return new InstallationState();
        }
    }

    public async Task SaveAsync(InstallationState state)
    {
        state.UpdatedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await _fileSystem.WriteAllTextAsync(StateFilePath, json, isSecret: true);
        await _fileSystem.SetPermissionsAsync(StateFilePath, "600");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
