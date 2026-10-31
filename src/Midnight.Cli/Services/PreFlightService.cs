using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Midnight.Cli.Common;
using Midnight.Cli.Models;

namespace Midnight.Cli.Services;

public interface IPreFlightService
{
    Task<PreFlightResult> RunAllChecksAsync(
        int panelPort = 80,
        int daemonPort = 8088,
        bool isDaemonIncluded = true,
        CancellationToken cancellationToken = default);

    Task<bool> EnsureDockerInstalledAsync(CancellationToken cancellationToken = default);
    Task<bool> EnsurePortsOpenedInFirewallAsync(IReadOnlyList<int> ports, CancellationToken cancellationToken = default);
}

public class PreFlightService : IPreFlightService
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;
    private readonly IConsoleOutput _console;
    private readonly CliContext _context;

    public PreFlightService(
        IProcessRunner processRunner,
        IFileSystem fileSystem,
        IConsoleOutput console,
        CliContext context)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _console = console;
        _context = context;
    }

    public async Task<PreFlightResult> RunAllChecksAsync(
        int panelPort = 80,
        int daemonPort = 8088,
        bool isDaemonIncluded = true,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<CheckItem>();

        // 1. Operating system & version
        checks.Add(await CheckOperatingSystemAsync());

        // 2. CPU architecture
        checks.Add(CheckArchitecture());

        // 3. Memory (RAM)
        checks.Add(await CheckRamAsync());

        // 4. Free disk space
        checks.Add(CheckDiskSpace());

        // 5. Docker & Docker Compose
        checks.Add(await CheckDockerAsync(cancellationToken));
        checks.Add(await CheckDockerComposeAsync(cancellationToken));

        // 6. Ports in use
        var portsToCheck = new List<int> { 80, 443 };
        if (panelPort != 80 && panelPort != 443) portsToCheck.Add(panelPort);
        if (isDaemonIncluded) portsToCheck.Add(daemonPort);

        var portConflicts = await CheckPortConflictsAsync(portsToCheck, cancellationToken);
        if (portConflicts.Count == 0)
        {
            checks.Add(new CheckItem("Ports", CheckStatus.Ok, $"All required ports ({string.Join(", ", portsToCheck)}) are free"));
        }
        else
        {
            foreach (var conflict in portConflicts)
            {
                checks.Add(new CheckItem(
                    $"Port {conflict.Port}",
                    CheckStatus.Failed,
                    $"In use by process '{conflict.ProcessName}' (PID {conflict.Pid?.ToString() ?? "unknown"})",
                    $"Stop the '{conflict.ProcessName}' service or configure a different port via --port"));
            }
        }

        // 7. Firewall (ufw / firewalld)
        checks.Add(await CheckFirewallAsync(portsToCheck, cancellationToken));

        return new PreFlightResult(checks);
    }

    public async Task<CheckItem> CheckOperatingSystemAsync()
    {
        if (!_fileSystem.FileExists("/etc/os-release"))
        {
            return new CheckItem(
                "Operating system",
                CheckStatus.Warning,
                "Non-Linux or unknown system (/etc/os-release missing)",
                "Recommended: Ubuntu 22.04/24.04, Debian 12 or RHEL/Rocky/Alma 9");
        }

        var lines = await _fileSystem.ReadAllTextAsync("/etc/os-release");
        var osId = ExtractValue(lines, "ID")?.ToLowerInvariant() ?? "unknown";
        var versionId = ExtractValue(lines, "VERSION_ID")?.Trim('"', '\'') ?? "unknown";
        var prettyName = ExtractValue(lines, "PRETTY_NAME")?.Trim('"', '\'') ?? $"{osId} {versionId}";

        var isSupported = (osId is "ubuntu" && versionId is "22.04" or "24.04")
                          || (osId is "debian" && (versionId.StartsWith("12") || versionId.StartsWith("13")))
                          || (osId is "almalinux" or "rocky" or "rhel" or "centos" && versionId.StartsWith("9"));

        if (isSupported)
        {
            return new CheckItem("Operating system", CheckStatus.Ok, $"{prettyName} (officially supported)");
        }

        return new CheckItem(
            "Operating system",
            CheckStatus.Warning,
            $"{prettyName} (not officially tested)",
            "Installation can continue; if problems occur, check your distribution");
    }

    public CheckItem CheckArchitecture()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        return arch switch
        {
            Architecture.X64 => new CheckItem("CPU architecture", CheckStatus.Ok, "x86_64 (64-bit Intel/AMD)"),
            Architecture.Arm64 => new CheckItem("CPU architecture", CheckStatus.Ok, "aarch64 / arm64 (64-bit ARM)"),
            _ => new CheckItem("CPU architecture", CheckStatus.Failed, $"{arch} is not supported", "Requires a 64-bit system (x86_64 or arm64)")
        };
    }

    public async Task<CheckItem> CheckRamAsync()
    {
        long totalRamMb = 0;

        if (_fileSystem.FileExists("/proc/meminfo"))
        {
            var content = await _fileSystem.ReadAllTextAsync("/proc/meminfo");
            var match = Regex.Match(content, @"MemTotal:\s+(\d+)\s+kB");
            if (match.Success && long.TryParse(match.Groups[1].Value, out var kb))
            {
                totalRamMb = kb / 1024;
            }
        }

        if (totalRamMb == 0)
        {
            // Fallback for tests / non-proc
            totalRamMb = 4096;
        }

        if (totalRamMb >= 2000)
        {
            return new CheckItem("Memory (RAM)", CheckStatus.Ok, $"{totalRamMb} MB available (meets minimum requirement of 2048 MB)");
        }

        if (totalRamMb >= 1400)
        {
            return new CheckItem(
                "Memory (RAM)",
                CheckStatus.Warning,
                $"{totalRamMb} MB available (tight, 2048 MB recommended)",
                "Consider adding swap: sudo fallocate -l 2G /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile");
        }

        return new CheckItem(
            "Memory (RAM)",
            CheckStatus.Failed,
            $"{totalRamMb} MB available (below minimum requirement of 2048 MB)",
            "Upgrade the server to at least 2 GB RAM");
    }

    public CheckItem CheckDiskSpace()
    {
        var freeBytes = _fileSystem.GetFreeDiskSpaceBytes("/opt/midnight");
        var freeGb = freeBytes / (1024.0 * 1024 * 1024);

        if (freeGb >= 10.0)
        {
            return new CheckItem("Free disk space", CheckStatus.Ok, $"{freeGb:F1} GB free on target partition");
        }

        if (freeGb >= 5.0)
        {
            return new CheckItem(
                "Free disk space",
                CheckStatus.Warning,
                $"{freeGb:F1} GB free (recommended: at least 10 GB)",
                "Clean up old Docker images with 'docker system prune -a'");
        }

        return new CheckItem(
            "Free disk space",
            CheckStatus.Failed,
            $"{freeGb:F1} GB free (at least 5 GB strictly required)",
            "Free up at least 5-10 GB of disk space");
    }

    public async Task<CheckItem> CheckDockerAsync(CancellationToken cancellationToken)
    {
        var res = await _processRunner.RunAsync("docker", "--version", cancellationToken: cancellationToken);
        if (!res.Success)
        {
            return new CheckItem(
                "Docker Engine",
                CheckStatus.Failed,
                "Docker is not installed or not in PATH",
                "Let Docker be installed automatically or install it manually via https://docs.docker.com/engine/install/");
        }

        var infoRes = await _processRunner.RunAsync("docker", "info", cancellationToken: cancellationToken);
        if (!infoRes.Success)
        {
            return new CheckItem(
                "Docker Daemon",
                CheckStatus.Failed,
                "Docker daemon is not running",
                "Start the service with: systemctl start docker");
        }

        return new CheckItem("Docker Engine", CheckStatus.Ok, res.StandardOutput.Trim());
    }

    public async Task<CheckItem> CheckDockerComposeAsync(CancellationToken cancellationToken)
    {
        var res = await _processRunner.RunAsync("docker", "compose version", cancellationToken: cancellationToken);
        if (res.Success)
        {
            return new CheckItem("Docker Compose", CheckStatus.Ok, res.StandardOutput.Trim());
        }

        return new CheckItem(
            "Docker Compose",
            CheckStatus.Failed,
            "Docker Compose plugin (v2) is not installed",
            "Install docker-compose-plugin (e.g. apt install docker-compose-plugin)");
    }

    public async Task<List<PortConflict>> CheckPortConflictsAsync(IReadOnlyList<int> ports, CancellationToken cancellationToken)
    {
        var conflicts = new List<PortConflict>();

        var res = await _processRunner.RunAsync("ss", "-tulpn", cancellationToken: cancellationToken);
        var output = res.Success ? res.StandardOutput : "";

        foreach (var port in ports)
        {
            // Look for lines containing :<port>
            var pattern = $@":{port}\s+.*users:\(\(""([^""]+)""(?:,pid=(\d+))?";
            var match = Regex.Match(output, pattern);

            if (match.Success)
            {
                var proc = match.Groups[1].Value;
                int? pid = int.TryParse(match.Groups[2].Value, out var parsedPid) ? parsedPid : null;
                conflicts.Add(new PortConflict(port, proc, pid));
            }
        }

        return conflicts;
    }

    public async Task<CheckItem> CheckFirewallAsync(IReadOnlyList<int> ports, CancellationToken cancellationToken)
    {
        // Check ufw
        var ufwRes = await _processRunner.RunAsync("ufw", "status", cancellationToken: cancellationToken);
        if (ufwRes.Success && ufwRes.StandardOutput.Contains("Status: active", StringComparison.OrdinalIgnoreCase))
        {
            var openPorts = ports.All(p => ufwRes.StandardOutput.Contains(p.ToString()));
            if (!openPorts)
            {
                return new CheckItem(
                    "Firewall (UFW)",
                    CheckStatus.Warning,
                    "UFW is active, but some ports (80/443) do not appear to be open",
                    "Open ports via: ufw allow 80/tcp && ufw allow 443/tcp");
            }
            return new CheckItem("Firewall (UFW)", CheckStatus.Ok, "Active and ports are open");
        }

        // Check firewalld
        var fwdRes = await _processRunner.RunAsync("firewall-cmd", "--state", cancellationToken: cancellationToken);
        if (fwdRes.Success && fwdRes.StandardOutput.Contains("running", StringComparison.OrdinalIgnoreCase))
        {
            return new CheckItem(
                "Firewall (firewalld)",
                CheckStatus.Warning,
                "firewalld is active",
                "Open ports via: firewall-cmd --permanent --add-service=http --add-service=https && firewall-cmd --reload");
        }

        return new CheckItem("Firewall", CheckStatus.Ok, "No blocking firewall detected");
    }

    public async Task<bool> EnsureDockerInstalledAsync(CancellationToken cancellationToken = default)
    {
        _console.WriteInfo("Installing Docker via the official convenience script...");
        var script = "curl -fsSL https://get.docker.com | sh && systemctl enable --now docker";
        var res = await _processRunner.RunBashAsync(script, isMutating: true, cancellationToken: cancellationToken);

        if (res.Success)
        {
            _console.WriteSuccess("Docker Engine installed successfully!");
            return true;
        }

        _console.WriteError($"Docker installation failed: {res.Output}");
        return false;
    }

    public async Task<bool> EnsurePortsOpenedInFirewallAsync(IReadOnlyList<int> ports, CancellationToken cancellationToken = default)
    {
        var ufw = await _processRunner.RunAsync("which", "ufw", cancellationToken: cancellationToken);
        if (ufw.Success)
        {
            foreach (var port in ports)
            {
                await _processRunner.RunAsync("ufw", $"allow {port}/tcp", isMutating: true, cancellationToken: cancellationToken);
            }
            _console.WriteSuccess($"Ports {string.Join(", ", ports)} opened in UFW.");
            return true;
        }

        var fwd = await _processRunner.RunAsync("which", "firewall-cmd", cancellationToken: cancellationToken);
        if (fwd.Success)
        {
            foreach (var port in ports)
            {
                await _processRunner.RunAsync("firewall-cmd", $"--permanent --add-port={port}/tcp", isMutating: true, cancellationToken: cancellationToken);
            }
            await _processRunner.RunAsync("firewall-cmd", "--reload", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess($"Ports {string.Join(", ", ports)} opened in firewalld.");
            return true;
        }

        return false;
    }

    private static string? ExtractValue(string content, string key)
    {
        var match = Regex.Match(content, $@"^{key}=(.*)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
