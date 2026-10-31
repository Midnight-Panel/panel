using Midnight.Cli.Common;
using Midnight.Cli.Models;
using Midnight.Cli.Services;
using Midnight.Cli.Tests.Mocks;
using Xunit;

namespace Midnight.Cli.Tests;

public class PreFlightServiceTests
{
    private readonly MockProcessRunner _processRunner = new();
    private readonly MockFileSystem _fileSystem = new();
    private readonly MockConsoleOutput _console = new();
    private readonly CliContext _context = new();

    private PreFlightService CreateService() =>
        new(_processRunner, _fileSystem, _console, _context);

    [Theory]
    [InlineData("ubuntu", "22.04", CheckStatus.Ok)]
    [InlineData("ubuntu", "24.04", CheckStatus.Ok)]
    [InlineData("debian", "12", CheckStatus.Ok)]
    [InlineData("almalinux", "9.4", CheckStatus.Ok)]
    [InlineData("arch", "rolling", CheckStatus.Warning)]
    [InlineData("fedora", "40", CheckStatus.Warning)]
    public async Task CheckOperatingSystem_EvaluatesDistributionsCorrectly(string id, string version, CheckStatus expectedStatus)
    {
        // Arrange
        var service = CreateService();
        _fileSystem.Files["/etc/os-release"] = $"ID={id}\nVERSION_ID=\"{version}\"\nPRETTY_NAME=\"{id} {version}\"";

        // Act
        var result = await service.CheckOperatingSystemAsync();

        // Assert
        Assert.Equal(expectedStatus, result.Status);
    }

    [Fact]
    public async Task CheckRam_FailsWhenBelowMinimumRequirements()
    {
        // Arrange: 1024 MB RAM (below the 1500 MB minimum)
        var service = CreateService();
        _fileSystem.Files["/proc/meminfo"] = "MemTotal:        1048576 kB\n";

        // Act
        var result = await service.CheckRamAsync();

        // Assert
        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("minimum requirement", result.Details);
    }

    [Fact]
    public async Task CheckRam_WarnsWhenMarginal()
    {
        // Arrange: 1700 MB RAM (between 1400 and 2000 MB)
        var service = CreateService();
        _fileSystem.Files["/proc/meminfo"] = "MemTotal:        1740800 kB\n";

        // Act
        var result = await service.CheckRamAsync();

        // Assert
        Assert.Equal(CheckStatus.Warning, result.Status);
    }

    [Fact]
    public async Task CheckRam_SucceedsWhenSufficient()
    {
        // Arrange: 4096 MB RAM
        var service = CreateService();
        _fileSystem.Files["/proc/meminfo"] = "MemTotal:        4194304 kB\n";

        // Act
        var result = await service.CheckRamAsync();

        // Assert
        Assert.Equal(CheckStatus.Ok, result.Status);
    }

    [Fact]
    public void CheckDiskSpace_FailsWhenUnder5GB()
    {
        // Arrange: 3 GB free
        var service = CreateService();
        _fileSystem.FreeDiskSpaceBytes = 3L * 1024 * 1024 * 1024;

        // Act
        var result = service.CheckDiskSpace();

        // Assert
        Assert.Equal(CheckStatus.Failed, result.Status);
    }

    [Fact]
    public async Task CheckPortConflicts_DetectsOccupiedPortAndProcessName()
    {
        // Arrange
        var service = CreateService();
        _processRunner.CustomResults["ss -tulpn"] = new ProcessResult(
            0,
            "tcp LISTEN 0 128 0.0.0.0:80 0.0.0.0:* users:((\"apache2\",pid=1420,fd=4))\n",
            string.Empty);

        // Act
        var conflicts = await service.CheckPortConflictsAsync([80, 443], CancellationToken.None);

        // Assert
        Assert.Single(conflicts);
        Assert.Equal(80, conflicts[0].Port);
        Assert.Equal("apache2", conflicts[0].ProcessName);
        Assert.Equal(1420, conflicts[0].Pid);
    }
}
