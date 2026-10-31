using Midnight.Cli.Common;
using Midnight.Cli.Services;
using Midnight.Cli.Tests.Mocks;
using Xunit;

namespace Midnight.Cli.Tests;

public class DoctorServiceTests
{
    private readonly MockProcessRunner _processRunner = new();
    private readonly MockFileSystem _fileSystem = new();
    private readonly MockConsoleOutput _console = new();
    private readonly CliContext _context = new();
    private readonly PreFlightService _preFlightService;

    public DoctorServiceTests()
    {
        _preFlightService = new PreFlightService(_processRunner, _fileSystem, _console, _context);
        _fileSystem.Files["/etc/os-release"] = "ID=ubuntu\nVERSION_ID=\"24.04\"\n";
        _fileSystem.Files["/proc/meminfo"] = "MemTotal: 8388608 kB\n";
    }

    private DoctorService CreateService() =>
        new(_preFlightService, _fileSystem, _processRunner, _console, _context);

    [Fact]
    public async Task Doctor_WarnsWhenSensitiveFileHasPermissiveMode()
    {
        // Arrange: .env hat 644 statt 600
        var service = CreateService();
        _fileSystem.Files["/opt/midnight/panel/.env"] = "DB_PASS=123";
        _fileSystem.Permissions["/opt/midnight/panel/.env"] = "644";

        // Act
        var result = await service.RunDoctorAsync();

        // Assert: Hat Warnung bzgl. Dateirechte gemeldet
        var envCheck = _console.CheckedItems.FirstOrDefault(i => i.Name.Contains(".env"));
        Assert.NotNull(envCheck);
        Assert.Equal(CheckStatus.Warning, envCheck.Status);
        Assert.Contains("Rechte sind 644", envCheck.Details);
        Assert.Contains("chmod 600", envCheck.Remediation);
    }

    [Fact]
    public async Task Doctor_SucceedsWhenSensitiveFileHas600Mode()
    {
        // Arrange: .env and midnight.json are strictly 600
        var service = CreateService();
        _fileSystem.Files["/opt/midnight/panel/.env"] = "DB_PASS=123";
        _fileSystem.Permissions["/opt/midnight/panel/.env"] = "600";

        _fileSystem.Files["/etc/midnight/midnight.json"] = "{}";
        _fileSystem.Permissions["/etc/midnight/midnight.json"] = "600";

        // Act
        var result = await service.RunDoctorAsync();

        // Assert
        var envCheck = _console.CheckedItems.FirstOrDefault(i => i.Name.Contains(".env"));
        Assert.NotNull(envCheck);
        Assert.Equal(CheckStatus.Ok, envCheck.Status);
    }
}

public class SecretScrubberTests
{
    [Theory]
    [InlineData("POSTGRES_PASSWORD=supersecret", "POSTGRES_PASSWORD=********")]
    [InlineData("MIDNIGHT_ADMIN_PASSWORD=myadminpwd", "MIDNIGHT_ADMIN_PASSWORD=********")]
    [InlineData("Host=db;Password=secretpass;User=midnight", "Host=db;Password=********;User=midnight")]
    [InlineData("curl -H 'Authorization: Bearer my_secret_token_123'", "curl -H 'Authorization: Bearer my_secret_token_123'")]
    [InlineData("midnight install --password my_secret_pwd", "midnight install --password ********")]
    public void Redact_HidesPasswordsAndSecrets(string input, string expected)
    {
        var actual = SecretScrubber.Redact(input);
        Assert.Equal(expected, actual);
    }
}
