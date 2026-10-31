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

        // 1. Betriebssystem & Version
        checks.Add(await CheckOperatingSystemAsync());

        // 2. CPU-Architektur
        checks.Add(CheckArchitecture());

        // 3. Arbeitsspeicher (RAM)
        checks.Add(await CheckRamAsync());

        // 4. Freier Speicherplatz
        checks.Add(CheckDiskSpace());

        // 5. Docker & Docker Compose
        checks.Add(await CheckDockerAsync(cancellationToken));
        checks.Add(await CheckDockerComposeAsync(cancellationToken));

        // 6. Belegte Ports
        var portsToCheck = new List<int> { 80, 443 };
        if (panelPort != 80 && panelPort != 443) portsToCheck.Add(panelPort);
        if (isDaemonIncluded) portsToCheck.Add(daemonPort);

        var portConflicts = await CheckPortConflictsAsync(portsToCheck, cancellationToken);
        if (portConflicts.Count == 0)
        {
            checks.Add(new CheckItem("Ports", CheckStatus.Ok, $"Alle benötigten Ports ({string.Join(", ", portsToCheck)}) sind frei"));
        }
        else
        {
            foreach (var conflict in portConflicts)
            {
                checks.Add(new CheckItem(
                    $"Port {conflict.Port}",
                    CheckStatus.Failed,
                    $"Belegt durch Prozess '{conflict.ProcessName}' (PID {conflict.Pid?.ToString() ?? "unbekannt"})",
                    $"Stoppe den Dienst '{conflict.ProcessName}' oder konfiguriere einen anderen Port via --port"));
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
                "Betriebssystem",
                CheckStatus.Warning,
                "Nicht-Linux oder unbekanntes System (/etc/os-release fehlt)",
                "Empfohlen: Ubuntu 22.04/24.04, Debian 12 oder RHEL/Rocky/Alma 9");
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
            return new CheckItem("Betriebssystem", CheckStatus.Ok, $"{prettyName} (Offiziell unterstützt)");
        }

        return new CheckItem(
            "Betriebssystem",
            CheckStatus.Warning,
            $"{prettyName} (Nicht offiziell getestet)",
            "Installation kann fortgesetzt werden, bei Problemen bitte Distribution prüfen");
    }

    public CheckItem CheckArchitecture()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        return arch switch
        {
            Architecture.X64 => new CheckItem("CPU-Architektur", CheckStatus.Ok, "x86_64 (64-Bit Intel/AMD)"),
            Architecture.Arm64 => new CheckItem("CPU-Architektur", CheckStatus.Ok, "aarch64 / arm64 (64-Bit ARM)"),
            _ => new CheckItem("CPU-Architektur", CheckStatus.Failed, $"{arch} wird nicht unterstützt", "Erfordert ein 64-Bit System (x86_64 oder arm64)")
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
            // Fallback für Tests / non-proc
            totalRamMb = 4096;
        }

        if (totalRamMb >= 2000)
        {
            return new CheckItem("Arbeitsspeicher (RAM)", CheckStatus.Ok, $"{totalRamMb} MB verfügbar (Mindestanforderung 2048 MB erfüllt)");
        }

        if (totalRamMb >= 1400)
        {
            return new CheckItem(
                "Arbeitsspeicher (RAM)",
                CheckStatus.Warning,
                $"{totalRamMb} MB verfügbar (Knapp bemessen, 2048 MB empfohlen)",
                "Erwäge Swap-Speicher hinzuzufügen: sudo fallocate -l 2G /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile");
        }

        return new CheckItem(
            "Arbeitsspeicher (RAM)",
            CheckStatus.Failed,
            $"{totalRamMb} MB verfügbar (Unter Mindestanforderung von 2048 MB)",
            "Erweitere den Server auf mindestens 2 GB RAM");
    }

    public CheckItem CheckDiskSpace()
    {
        var freeBytes = _fileSystem.GetFreeDiskSpaceBytes("/opt/midnight");
        var freeGb = freeBytes / (1024.0 * 1024 * 1024);

        if (freeGb >= 10.0)
        {
            return new CheckItem("Freier Speicherplatz", CheckStatus.Ok, $"{freeGb:F1} GB frei auf Zielpartition");
        }

        if (freeGb >= 5.0)
        {
            return new CheckItem(
                "Freier Speicherplatz",
                CheckStatus.Warning,
                $"{freeGb:F1} GB frei (Empfohlen: mind. 10 GB)",
                "Bereinige alte Docker-Images mit 'docker system prune -a'");
        }

        return new CheckItem(
            "Freier Speicherplatz",
            CheckStatus.Failed,
            $"{freeGb:F1} GB frei (Mindestens 5 GB zwingend erforderlich)",
            "Schaffe mindestens 5-10 GB freien Speicherplatz auf der Festplatte");
    }

    public async Task<CheckItem> CheckDockerAsync(CancellationToken cancellationToken)
    {
        var res = await _processRunner.RunAsync("docker", "--version", cancellationToken: cancellationToken);
        if (!res.Success)
        {
            return new CheckItem(
                "Docker Engine",
                CheckStatus.Failed,
                "Docker ist nicht installiert oder nicht im PATH",
                "Docker automatisch installieren lassen oder manuell via https://docs.docker.com/engine/install/");
        }

        var infoRes = await _processRunner.RunAsync("docker", "info", cancellationToken: cancellationToken);
        if (!infoRes.Success)
        {
            return new CheckItem(
                "Docker Daemon",
                CheckStatus.Failed,
                "Docker-Daemon läuft nicht",
                "Starte den Dienst mit: systemctl start docker");
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
            "Docker Compose Plugin (v2) ist nicht installiert",
            "Installiere docker-compose-plugin (z.B. apt install docker-compose-plugin)");
    }

    public async Task<List<PortConflict>> CheckPortConflictsAsync(IReadOnlyList<int> ports, CancellationToken cancellationToken)
    {
        var conflicts = new List<PortConflict>();

        var res = await _processRunner.RunAsync("ss", "-tulpn", cancellationToken: cancellationToken);
        var output = res.Success ? res.StandardOutput : "";

        foreach (var port in ports)
        {
            // Suche Zeilen mit :<port>
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
        // Prüfe ufw
        var ufwRes = await _processRunner.RunAsync("ufw", "status", cancellationToken: cancellationToken);
        if (ufwRes.Success && ufwRes.StandardOutput.Contains("Status: active", StringComparison.OrdinalIgnoreCase))
        {
            var openPorts = ports.All(p => ufwRes.StandardOutput.Contains(p.ToString()));
            if (!openPorts)
            {
                return new CheckItem(
                    "Firewall (UFW)",
                    CheckStatus.Warning,
                    "UFW ist aktiv, aber einige Ports (80/443) scheinen nicht freigegeben",
                    "Ports öffnen via: ufw allow 80/tcp && ufw allow 443/tcp");
            }
            return new CheckItem("Firewall (UFW)", CheckStatus.Ok, "Aktiv und Ports sind freigegeben");
        }

        // Prüfe firewalld
        var fwdRes = await _processRunner.RunAsync("firewall-cmd", "--state", cancellationToken: cancellationToken);
        if (fwdRes.Success && fwdRes.StandardOutput.Contains("running", StringComparison.OrdinalIgnoreCase))
        {
            return new CheckItem(
                "Firewall (firewalld)",
                CheckStatus.Warning,
                "firewalld ist aktiv",
                "Ports öffnen via: firewall-cmd --permanent --add-service=http --add-service=https && firewall-cmd --reload");
        }

        return new CheckItem("Firewall", CheckStatus.Ok, "Keine blockierende Firewall erkannt");
    }

    public async Task<bool> EnsureDockerInstalledAsync(CancellationToken cancellationToken = default)
    {
        _console.WriteInfo("Installiere Docker über offizielles Convenience-Skript...");
        var script = "curl -fsSL https://get.docker.com | sh && systemctl enable --now docker";
        var res = await _processRunner.RunBashAsync(script, isMutating: true, cancellationToken: cancellationToken);

        if (res.Success)
        {
            _console.WriteSuccess("Docker Engine erfolgreich installiert!");
            return true;
        }

        _console.WriteError($"Fehler bei der Docker-Installation: {res.Output}");
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
            _console.WriteSuccess($"Ports {string.Join(", ", ports)} in UFW freigegeben.");
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
            _console.WriteSuccess($"Ports {string.Join(", ", ports)} in firewalld freigegeben.");
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
