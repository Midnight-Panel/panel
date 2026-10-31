using System.Security.Cryptography;
using Midnight.Cli.Common;
using Midnight.Cli.Models;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class InstallCommand : ICommand
{
    private readonly IPreFlightService _preFlightService;
    private readonly IStateService _stateService;
    private readonly IFileSystem _fileSystem;
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;
    private readonly CliContext _context;

    public string Name => "install";
    public string Description => "Installiere Midnight Panel und/oder Daemon inklusive Pre-Flight-Checks";

    public InstallCommand(
        IPreFlightService preFlightService,
        IStateService stateService,
        IFileSystem fileSystem,
        IProcessRunner processRunner,
        IConsoleOutput console,
        CliContext context)
    {
        _preFlightService = preFlightService;
        _stateService = stateService;
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _console = console;
        _context = context;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var target = args.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? "both";
        var isDaemon = target is "daemon" or "both";
        var isPanel = target is "panel" or "both";

        _console.WriteHeader($"Midnight Installer (Komponenten: {target})");

        // 1. Idempotenz-Prüfung: Bestehende Installation erkennen
        if (_stateService.Exists())
        {
            var existingState = await _stateService.LoadAsync();
            if (existingState.PanelInstalled || existingState.DaemonInstalled)
            {
                _console.WriteWarning("Es wurde eine bestehende Midnight-Installation erkannt!");
                _console.WriteLine($"Aktueller Stand: Panel {existingState.PanelVersion}, Daemon {existingState.DaemonVersion}");

                var choice = _console.SelectOption(
                    "Wie möchtest du verfahren?",
                    [
                        "Reparieren (Container neu aufsetzen & Verzeichnisse prüfen)",
                        "Neu konfigurieren (Domain, Ports oder Passwörter anpassen)",
                        "Abbrechen"
                    ],
                    defaultIndex: 0);

                if (choice == 2)
                {
                    _console.WriteInfo("Installation abgebrochen.");
                    return ExitCodes.Success;
                }

                if (choice == 1)
                {
                    _console.WriteInfo("Wechsle in den Konfigurationsmodus...");
                    // Fahre mit interaktiver Neukonfiguration fort
                }
            }
        }

        // 2. Pre-Flight-Checks durchführen
        _console.WriteInfo("Führe Pre-Flight-Systemprüfungen durch...");
        var preFlight = await _preFlightService.RunAllChecksAsync(
            panelPort: 80,
            daemonPort: 8088,
            isDaemonIncluded: isDaemon,
            cancellationToken: cancellationToken);

        _console.WriteChecklist("Pre-Flight Checks", preFlight.Checks);

        // Docker-Installation anbieten falls fehlend
        var dockerCheck = preFlight.Checks.FirstOrDefault(c => c.Name.Contains("Docker"));
        if (dockerCheck != null && dockerCheck.Status == CheckStatus.Failed)
        {
            var installDocker = _console.Confirm("Docker ist nicht installiert. Soll Docker jetzt automatisch installiert werden?", defaultYes: true);
            if (installDocker)
            {
                var dockerInstalled = await _preFlightService.EnsureDockerInstalledAsync(cancellationToken);
                if (!dockerInstalled)
                {
                    return ExitCodes.PreFlightFailed;
                }
            }
            else
            {
                _console.WriteError("Installation kann ohne Docker nicht fortgesetzt werden.");
                return ExitCodes.PreFlightFailed;
            }
        }

        // Firewall-Öffnung anbieten
        var fwCheck = preFlight.Checks.FirstOrDefault(c => c.Name.Contains("Firewall"));
        if (fwCheck != null && fwCheck.Status == CheckStatus.Warning)
        {
            var openFw = _console.Confirm("Sollen die benötigten Web-Ports (80, 443) automatisch in der Firewall geöffnet werden?", defaultYes: true);
            if (openFw)
            {
                await _preFlightService.EnsurePortsOpenedInFirewallAsync([80, 443], cancellationToken);
            }
        }

        if (!preFlight.CanProceed)
        {
            _console.WriteError($"Pre-Flight-Checks fehlgeschlagen ({preFlight.ErrorCount} Fehler). Behebe die Fehler und starte erneut.");
            return ExitCodes.PreFlightFailed;
        }

        if (preFlight.HasWarnings)
        {
            var proceedWithWarnings = _console.Confirm("Es gibt Warnungen. Möchtest du trotzdem fortfahren?", defaultYes: true);
            if (!proceedWithWarnings)
            {
                return ExitCodes.Cancelled;
            }
        }

        // 3. Konfigurationsparameter abfragen
        var domain = _console.Ask("Domain für das Panel (z. B. panel.deine-domain.de)", "localhost");
        var dbPassword = GenerateRandomSecret(32);

        // 4. Zielverzeichnisse anlegen
        var panelDir = "/opt/midnight/panel";
        _fileSystem.CreateDirectory(panelDir);
        _fileSystem.CreateDirectory($"{panelDir}/storage");

        // 5. .env anlegen mit strikten Rechten (600, nur root)
        var envPath = $"{panelDir}/.env";
        var envContent = $"""
        PANEL_DOMAIN={domain}
        POSTGRES_PASSWORD={dbPassword}
        MIDNIGHT_Database__ConnectionString=Host=db;Database=midnight;Username=midnight;Password={dbPassword}
        MIDNIGHT_PublicUrl=https://{domain}
        """;
        await _fileSystem.WriteAllTextAsync(envPath, envContent, isSecret: true);
        await _fileSystem.SetPermissionsAsync(envPath, "600");
        _console.WriteSuccess(".env-Konfiguration mit sicheren Rechten (600) angelegt.");

        // 6. Docker-Compose Datei kopieren/bereitstellen
        var composeSource = "/opt/midnight/src/panel/docker-compose.yml";
        var composeTarget = $"{panelDir}/docker-compose.yml";
        if (_fileSystem.FileExists(composeSource))
        {
            var compose = await _fileSystem.ReadAllTextAsync(composeSource);
            await _fileSystem.WriteAllTextAsync(composeTarget, compose);
        }

        // 7. Stack starten
        if (isPanel)
        {
            _console.WriteInfo("Starte Midnight Panel Docker-Compose-Stack...");
            var upRes = await _processRunner.RunAsync(
                "docker",
                $"compose -f {composeTarget} up -d",
                workingDirectory: panelDir,
                isMutating: true,
                cancellationToken: cancellationToken);

            if (!upRes.Success && !_context.DryRun)
            {
                _console.WriteError($"Fehler beim Starten des Docker-Compose-Stacks: {upRes.Output}");
                _console.ShowLogTail(_context.LogFilePath);
                return ExitCodes.GeneralError;
            }
        }

        // 8. Daemon-Dienst einrichten falls angefordert
        if (isDaemon)
        {
            _console.WriteInfo("Richte Midnight Daemon als systemd-Dienst ein...");
            var daemonServicePath = "/etc/systemd/system/midnight-daemon.service";
            var serviceContent = """
            [Unit]
            Description=Midnight Daemon
            After=network.target docker.service
            Requires=docker.service

            [Service]
            Type=simple
            User=root
            WorkingDirectory=/opt/midnight/daemon
            ExecStart=/opt/midnight/daemon/Midnight.Daemon
            Restart=always
            RestartSec=5

            [Install]
            WantedBy=multi-user.target
            """;
            await _fileSystem.WriteAllTextAsync(daemonServicePath, serviceContent);
            await _processRunner.RunAsync("systemctl", "daemon-reload", isMutating: true, cancellationToken: cancellationToken);
            await _processRunner.RunAsync("systemctl", "enable --now midnight-daemon", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("midnight-daemon Dienst erfolgreich registriert und gestartet.");
        }

        // 9. Installationszustand in /etc/midnight/midnight.json sichern
        var newState = new InstallationState
        {
            PanelInstalled = isPanel,
            DaemonInstalled = isDaemon,
            PanelVersion = "v0.1.0",
            DaemonVersion = "v0.1.0",
            PanelDomain = domain,
            PanelPort = 80,
            DaemonPort = 8088,
            InstalledAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _stateService.SaveAsync(newState);

        _console.WriteHeader("Installation erfolgreich");
        _console.WriteSuccess($"Midnight Panel ist online erreichbar unter: https://{domain}");
        _console.WriteInfo("Erstelle einen Administrator-Account mit: midnight create-admin");
        return ExitCodes.Success;
    }

    private static string GenerateRandomSecret(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..length];
    }
}
