using Midnight.Cli.Common;
using Midnight.Cli.Models;
using Midnight.Cli.Services;
using Midnight.Cli.Tests.Mocks;
using Xunit;

namespace Midnight.Cli.Tests;

public class UpdateServiceTests
{
    private readonly MockProcessRunner _processRunner = new();
    private readonly MockFileSystem _fileSystem = new();
    private readonly MockConsoleOutput _console = new();
    private readonly CliContext _context = new();
    private readonly StateService _stateService;
    private readonly BackupService _backupService;
    private readonly RestoreService _restoreService;

    public UpdateServiceTests()
    {
        _stateService = new StateService(_fileSystem, _context);
        _backupService = new BackupService(_processRunner, _fileSystem, _stateService, _console, _context);
        _restoreService = new RestoreService(_processRunner, _fileSystem, _backupService, _stateService, _console, _context);

        _fileSystem.Directories.Add("/opt/midnight/panel");
        _fileSystem.Directories.Add("/opt/midnight/src/panel");
        _fileSystem.Files["/etc/midnight/midnight.json"] = "{\"PanelVersion\":\"v0.1.0\"}";
    }

    private UpdateService CreateService() =>
        new(_processRunner, _fileSystem, _backupService, _restoreService, _stateService, _console, _context);

    [Fact]
    public async Task UpdatePanel_CreatesPreUpdateBackupAndSucceedsOnHealthyCheck()
    {
        // Arrange
        var service = CreateService();
        _processRunner.CustomResults["curl -fsS http://localhost:8080/api/health"] =
            new ProcessResult(0, "{\"status\":\"healthy\"}", string.Empty);

        // Act
        var result = await service.UpdatePanelAsync("v0.2.0");

        // Assert
        Assert.True(result);
        var state = await _stateService.LoadAsync();
        Assert.Equal("v0.2.0", state.PanelVersion);
        Assert.Equal("v0.1.0", state.PreviousPanelVersion);
        Assert.NotNull(state.LastBackupPath);
        Assert.Contains("midnight-backup-pre-update", state.LastBackupPath);
    }

    [Fact]
    public async Task UpdatePanel_TriggersAutoRollbackWhenHealthCheckFails()
    {
        // Arrange
        var service = CreateService();
        // Health-Check schlägt fehl (Exit-Code 7 / Verbindung abgelehnt)
        _processRunner.CustomResults["curl -fsS http://localhost:8080/api/health"] =
            new ProcessResult(7, string.Empty, "Connection refused");

        // Act
        var result = await service.UpdatePanelAsync("v0.2.0");

        // Assert
        Assert.False(result);
        Assert.Contains(_console.Messages, m => m.Contains("AUTOMATISCHEN ROLLBACK"));

        var state = await _stateService.LoadAsync();
        // Version muss wieder auf vorherige Version zurückgesetzt sein
        Assert.Equal("v0.1.0", state.PanelVersion);
    }

    [Fact]
    public async Task UpdatePanel_FallsBackToSourceBuildWhenRegistryImagePullFails()
    {
        // Arrange: Docker Pull schlägt fehl
        var service = CreateService();
        _processRunner.CustomResults["docker pull"] = new ProcessResult(1, string.Empty, "manifest unknown");
        _processRunner.CustomResults["curl -fsS http://localhost:8080/api/health"] =
            new ProcessResult(0, "{\"status\":\"healthy\"}", string.Empty);

        // Act
        var result = await service.UpdatePanelAsync("v0.2.0", useImages: true);

        // Assert
        Assert.True(result);
        Assert.Contains(_console.Messages, m => m.Contains("Wechsle automatisch zu Source-Build"));
        Assert.Contains(_processRunner.ExecutedCommands, c => c.Arguments.Contains("up -d --build"));
    }
}
