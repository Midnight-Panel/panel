using Midnight.Cli.Common;
using Midnight.Cli.Models;
using Midnight.Cli.Services;
using Midnight.Cli.Tests.Mocks;
using Xunit;

namespace Midnight.Cli.Tests;

public class BackupServiceTests
{
    private readonly MockProcessRunner _processRunner = new();
    private readonly MockFileSystem _fileSystem = new();
    private readonly MockConsoleOutput _console = new();
    private readonly CliContext _context = new();
    private readonly StateService _stateService;

    public BackupServiceTests()
    {
        _stateService = new StateService(_fileSystem, _context);
        _fileSystem.Directories.Add("/var/backups/midnight");
        _fileSystem.Directories.Add("/opt/midnight/panel");
    }

    private BackupService CreateService() =>
        new(_processRunner, _fileSystem, _stateService, _console, _context);

    [Fact]
    public async Task CreateBackup_CreatesArchiveWithVersionAndTimestamp()
    {
        // Arrange
        var service = CreateService();
        _fileSystem.Files["/etc/midnight/midnight.json"] = "{\"PanelVersion\":\"v0.1.0\"}";

        // Act
        var archivePath = await service.CreateBackupAsync("/var/backups/midnight", encrypt: false);

        // Assert
        Assert.StartsWith("/var/backups/midnight/midnight-backup-v0.1.0-", archivePath);
        Assert.EndsWith(".tar.gz", archivePath);
        Assert.True(_fileSystem.FileExists(archivePath));

        // Verify permissions are 600
        Assert.Equal("600", _fileSystem.Permissions[archivePath]);

        // Verify SHA-256 checksum
        var shaPath = $"{archivePath}.sha256";
        Assert.True(_fileSystem.FileExists(shaPath));
    }

    [Fact]
    public async Task CleanOldBackups_EnforcesRetentionKeepCount()
    {
        // Arrange: create 5 old backups
        var service = CreateService();
        var backupDir = "/var/backups/midnight";

        _fileSystem.Files[$"{backupDir}/midnight-backup-v0.1.0-20261001-120000.tar.gz"] = "data";
        _fileSystem.Files[$"{backupDir}/midnight-backup-v0.1.0-20261002-120000.tar.gz"] = "data";
        _fileSystem.Files[$"{backupDir}/midnight-backup-v0.1.0-20261003-120000.tar.gz"] = "data";
        _fileSystem.Files[$"{backupDir}/midnight-backup-v0.1.0-20261004-120000.tar.gz"] = "data";
        _fileSystem.Files[$"{backupDir}/midnight-backup-v0.1.0-20261005-120000.tar.gz"] = "data";

        // Act: keep only the 2 newest (--keep 2)
        await service.CleanOldBackupsAsync(backupDir, keepCount: 2);

        // Assert: the 3 oldest were deleted, the 2 newest are kept
        Assert.False(_fileSystem.FileExists($"{backupDir}/midnight-backup-v0.1.0-20261001-120000.tar.gz"));
        Assert.False(_fileSystem.FileExists($"{backupDir}/midnight-backup-v0.1.0-20261002-120000.tar.gz"));
        Assert.False(_fileSystem.FileExists($"{backupDir}/midnight-backup-v0.1.0-20261003-120000.tar.gz"));

        Assert.True(_fileSystem.FileExists($"{backupDir}/midnight-backup-v0.1.0-20261004-120000.tar.gz"));
        Assert.True(_fileSystem.FileExists($"{backupDir}/midnight-backup-v0.1.0-20261005-120000.tar.gz"));
    }
}
