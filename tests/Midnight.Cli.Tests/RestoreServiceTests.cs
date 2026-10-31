using Midnight.Cli.Common;
using Midnight.Cli.Services;
using Midnight.Cli.Tests.Mocks;
using Xunit;

namespace Midnight.Cli.Tests;

public class RestoreServiceTests
{
    private readonly MockProcessRunner _processRunner = new();
    private readonly MockFileSystem _fileSystem = new();
    private readonly MockConsoleOutput _console = new();
    private readonly CliContext _context = new();
    private readonly StateService _stateService;
    private readonly BackupService _backupService;

    public RestoreServiceTests()
    {
        _stateService = new StateService(_fileSystem, _context);
        _backupService = new BackupService(_processRunner, _fileSystem, _stateService, _console, _context);
        _fileSystem.Directories.Add("/opt/midnight/panel");
    }

    private RestoreService CreateService() =>
        new(_processRunner, _fileSystem, _backupService, _stateService, _console, _context);

    [Fact]
    public async Task Restore_FailsIfArchiveNotFound()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.RestoreAsync("/non/existent/backup.tar.gz");

        // Assert
        Assert.False(result);
        Assert.Contains(_console.Messages, m => m.Contains("not found"));
    }

    [Fact]
    public async Task Restore_FailsIfChecksumMismatch()
    {
        // Arrange
        var service = CreateService();
        var archive = "/var/backups/midnight/backup.tar.gz";
        _fileSystem.Files[archive] = "backup-content";
        _fileSystem.Files[$"{archive}.sha256"] = "wronghash  backup.tar.gz";
        _fileSystem.MockSha256 = "actualhash";

        // Act
        var result = await service.RestoreAsync(archive);

        // Assert
        Assert.False(result);
        Assert.Contains(_console.Messages, m => m.Contains("Checksum mismatch"));
    }

    [Fact]
    public async Task Restore_AbortsWhenUserRejectsConfirmation()
    {
        // Arrange
        var service = CreateService();
        var archive = "/var/backups/midnight/backup.tar.gz";
        _fileSystem.Files[archive] = "backup-content";
        _console.ConfirmResponse = false; // User says 'No'

        // Act
        var result = await service.RestoreAsync(archive);

        // Assert
        Assert.False(result);
        Assert.Contains(_console.Messages, m => m.Contains("aborted"));
    }

    [Fact]
    public async Task Restore_CreatesSafetyBackupAndRestoresSuccessfully()
    {
        // Arrange
        var service = CreateService();
        var archive = "/var/backups/midnight/backup.tar.gz";
        _fileSystem.Files[archive] = "backup-content";
        _console.ConfirmResponse = true;

        // Health-Check Mock
        _processRunner.CustomResults["curl -fsS http://localhost:8080/api/health"] =
            new ProcessResult(0, "{\"status\":\"healthy\"}", string.Empty);

        // Act
        var result = await service.RestoreAsync(archive);

        // Assert
        Assert.True(result);
        Assert.Contains(_console.Messages, m => m.Contains("Safety backup saved"));
        Assert.Contains(_console.Messages, m => m.Contains("Restore completed successfully"));

        // docker down and up were called
        Assert.Contains(_processRunner.ExecutedCommands, c => c.Arguments.Contains("down"));
        Assert.Contains(_processRunner.ExecutedCommands, c => c.Arguments.Contains("up -d"));
    }
}
