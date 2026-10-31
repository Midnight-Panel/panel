using System.Text.Json;
using Midnight.Cli.Common;
using Midnight.Cli.Models;

namespace Midnight.Cli.Services;

public interface IStatusService
{
    Task<StatusReport> GetStatusReportAsync(CancellationToken cancellationToken = default);
    Task PrintStatusAsync(bool jsonOutput = false, CancellationToken cancellationToken = default);
}

public class StatusService : IStatusService
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;
    private readonly IStateService _stateService;
    private readonly IConsoleOutput _console;

    public StatusService(
        IProcessRunner processRunner,
        IFileSystem fileSystem,
        IStateService stateService,
        IConsoleOutput console)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _stateService = stateService;
        _console = console;
    }

    public async Task<StatusReport> GetStatusReportAsync(CancellationToken cancellationToken = default)
    {
        var state = await _stateService.LoadAsync();
        var report = new StatusReport
        {
            PanelVersion = state.PanelVersion,
            PanelDomain = state.PanelDomain,
            DaemonVersion = state.DaemonVersion,
            Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString()
        };

        // Freier Speicherplatz
        var freeBytes = _fileSystem.GetFreeDiskSpaceBytes("/opt/midnight");
        report.DiskFreeSpace = $"{freeBytes / (1024.0 * 1024 * 1024):F1} GB";

        // Docker Compose Container Status abfragen
        var psRes = await _processRunner.RunAsync(
            "docker",
            "compose -f /opt/midnight/panel/docker-compose.yml ps --format json",
            cancellationToken: cancellationToken);

        if (psRes.Success && !string.IsNullOrWhiteSpace(psRes.StandardOutput))
        {
            report.PanelStatus = "Running";
            try
            {
                // Docker compose ps kann entweder ein JSON-Array oder Newline-delimitierte JSON-Objekte ausgeben
                var lines = psRes.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var name = root.TryGetProperty("Service", out var s) ? s.GetString() ?? "" : "";
                    var stateStr = root.TryGetProperty("State", out var st) ? st.GetString() ?? "" : "";
                    var status = root.TryGetProperty("Status", out var stat) ? stat.GetString() ?? "" : "";
                    var publishers = root.TryGetProperty("Publishers", out var pub) ? pub.ToString() : "";

                    report.Containers.Add(new ContainerStatusInfo(name, stateStr, status, publishers));

                    if (name == "db" && status.Contains("healthy", StringComparison.OrdinalIgnoreCase))
                    {
                        report.DatabaseHealth = "Healthy";
                    }
                }
            }
            catch
            {
                report.PanelStatus = "Active";
            }
        }
        else
        {
            report.PanelStatus = "Stopped";
            report.DatabaseHealth = "Offline";
        }

        // Daemon Systemd Status abfragen
        var daemonRes = await _processRunner.RunAsync("systemctl", "is-active midnight-daemon", cancellationToken: cancellationToken);
        report.DaemonStatus = daemonRes.Success ? daemonRes.StandardOutput.Trim() : "inactive";

        return report;
    }

    public async Task PrintStatusAsync(bool jsonOutput = false, CancellationToken cancellationToken = default)
    {
        var report = await GetStatusReportAsync(cancellationToken);

        if (jsonOutput)
        {
            _console.IsJsonMode = true;
            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(json);
            return;
        }

        _console.WriteHeader("Midnight System Status");

        _console.WriteLine($"Panel Version:   {report.PanelVersion} (Domain: {report.PanelDomain ?? "n/a"})");
        _console.WriteLine($"Panel Status:    {(report.PanelStatus == "Running" ? "[✓] Online" : "[✗] Offline")}");
        _console.WriteLine($"Datenbank:       {(report.DatabaseHealth == "Healthy" ? "[✓] Healthy" : "[!] " + report.DatabaseHealth)}");
        _console.WriteLine($"Daemon Status:   {(report.DaemonStatus == "active" ? "[✓] Active" : "[!] " + report.DaemonStatus)} ({report.DaemonVersion})");
        _console.WriteLine($"Speicherplatz:   {report.DiskFreeSpace} frei auf /opt/midnight");
        _console.WriteLine($"Architektur:     {report.Architecture}");
        _console.WriteLine();

        if (report.Containers.Count > 0)
        {
            _console.WriteLine("CONTAINER                STATUS               STATE");
            _console.WriteLine("─────────────────────────────────────────────────────────");
            foreach (var c in report.Containers)
            {
                _console.WriteLine($"{c.Name,-24} {c.Status,-20} {c.State}");
            }
            _console.WriteLine();
        }
    }
}
