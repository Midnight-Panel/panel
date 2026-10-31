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
    public string Description => "Install Midnight Panel and/or Demon, including pre-flight checks";

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
        var isDemon = target is "demon" or "both";
        var isPanel = target is "panel" or "both";

        _console.WriteHeader($"Midnight Installer (components: {target})");

        // 1. Idempotency check: detect an existing installation
        if (_stateService.Exists())
        {
            var existingState = await _stateService.LoadAsync();
            if (existingState.PanelInstalled || existingState.DemonInstalled)
            {
                _console.WriteWarning("An existing Midnight installation was detected!");
                _console.WriteLine($"Current state: Panel {existingState.PanelVersion}, Demon {existingState.DemonVersion}");

                var choice = _console.SelectOption(
                    "How do you want to proceed?",
                    [
                        "Repair (recreate containers & check directories)",
                        "Reconfigure (change domain, ports or passwords)",
                        "Cancel"
                    ],
                    defaultIndex: 0);

                if (choice == 2)
                {
                    _console.WriteInfo("Installation cancelled.");
                    return ExitCodes.Success;
                }

                if (choice == 1)
                {
                    _console.WriteInfo("Switching to configuration mode...");
                    // Continue with interactive reconfiguration
                }
            }
        }

        // 2. Run pre-flight checks
        _console.WriteInfo("Running pre-flight system checks...");
        var preFlight = await _preFlightService.RunAllChecksAsync(
            panelPort: 80,
            demonPort: 8088,
            isDemonIncluded: isDemon,
            cancellationToken: cancellationToken);

        _console.WriteChecklist("Pre-Flight Checks", preFlight.Checks);

        // Offer Docker installation if missing
        var dockerCheck = preFlight.Checks.FirstOrDefault(c => c.Name.Contains("Docker"));
        if (dockerCheck != null && dockerCheck.Status == CheckStatus.Failed)
        {
            var installDocker = _console.Confirm("Docker is not installed. Install Docker automatically now?", defaultYes: true);
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
                _console.WriteError("Installation cannot continue without Docker.");
                return ExitCodes.PreFlightFailed;
            }
        }

        // Offer to open the firewall
        var fwCheck = preFlight.Checks.FirstOrDefault(c => c.Name.Contains("Firewall"));
        if (fwCheck != null && fwCheck.Status == CheckStatus.Warning)
        {
            var openFw = _console.Confirm("Open the required web ports (80, 443) in the firewall automatically?", defaultYes: true);
            if (openFw)
            {
                await _preFlightService.EnsurePortsOpenedInFirewallAsync([80, 443], cancellationToken);
            }
        }

        if (!preFlight.CanProceed)
        {
            _console.WriteError($"Pre-flight checks failed ({preFlight.ErrorCount} errors). Fix the errors and run again.");
            return ExitCodes.PreFlightFailed;
        }

        if (preFlight.HasWarnings)
        {
            var proceedWithWarnings = _console.Confirm("There are warnings. Do you want to continue anyway?", defaultYes: true);
            if (!proceedWithWarnings)
            {
                return ExitCodes.Cancelled;
            }
        }

        // 3. Ask for configuration parameters
        var domain = _console.Ask("Domain for the panel (e.g. panel.your-domain.com)", "localhost");
        var dbPassword = GenerateRandomSecret(32);

        // 4. Create target directories
        var panelDir = "/opt/midnight/panel";
        _fileSystem.CreateDirectory(panelDir);
        _fileSystem.CreateDirectory($"{panelDir}/storage");

        // 5. Create .env with strict permissions (600, root only)
        var envPath = $"{panelDir}/.env";
        var envContent = $"""
        PANEL_DOMAIN={domain}
        POSTGRES_PASSWORD={dbPassword}
        MIDNIGHT_Database__ConnectionString=Host=db;Database=midnight;Username=midnight;Password={dbPassword}
        MIDNIGHT_PublicUrl=https://{domain}
        """;
        await _fileSystem.WriteAllTextAsync(envPath, envContent, isSecret: true);
        await _fileSystem.SetPermissionsAsync(envPath, "600");
        _console.WriteSuccess(".env configuration created with secure permissions (600).");

        // 6. Copy/provide the Docker Compose file
        var composeSource = "/opt/midnight/src/panel/docker-compose.yml";
        var composeTarget = $"{panelDir}/docker-compose.yml";
        if (_fileSystem.FileExists(composeSource))
        {
            var compose = await _fileSystem.ReadAllTextAsync(composeSource);
            await _fileSystem.WriteAllTextAsync(composeTarget, compose);
        }

        // 7. Start the stack
        if (isPanel)
        {
            _console.WriteInfo("Starting Midnight Panel Docker Compose stack...");
            var upRes = await _processRunner.RunAsync(
                "docker",
                $"compose -f {composeTarget} up -d",
                workingDirectory: panelDir,
                isMutating: true,
                cancellationToken: cancellationToken);

            if (!upRes.Success && !_context.DryRun)
            {
                _console.WriteError($"Failed to start the Docker Compose stack: {upRes.Output}");
                _console.ShowLogTail(_context.LogFilePath);
                return ExitCodes.GeneralError;
            }
        }

        // 8. Set up the demon service if requested
        if (isDemon)
        {
            _console.WriteInfo("Setting up Midnight Demon as a systemd service...");
            var demonServicePath = "/etc/systemd/system/midnight-demon.service";
            var serviceContent = """
            [Unit]
            Description=Midnight Demon
            After=network.target docker.service
            Requires=docker.service

            [Service]
            Type=simple
            User=root
            WorkingDirectory=/opt/midnight/demon
            ExecStart=/opt/midnight/demon/Midnight.Demon
            Restart=always
            RestartSec=5

            [Install]
            WantedBy=multi-user.target
            """;
            await _fileSystem.WriteAllTextAsync(demonServicePath, serviceContent);
            await _processRunner.RunAsync("systemctl", "daemon-reload", isMutating: true, cancellationToken: cancellationToken);
            await _processRunner.RunAsync("systemctl", "enable --now midnight-demon", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("midnight-demon service registered and started successfully.");
        }

        // 9. Save installation state to /etc/midnight/midnight.json
        var newState = new InstallationState
        {
            PanelInstalled = isPanel,
            DemonInstalled = isDemon,
            PanelVersion = "v0.1.0",
            DemonVersion = "v0.1.0",
            PanelDomain = domain,
            PanelPort = 80,
            DemonPort = 8088,
            InstalledAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _stateService.SaveAsync(newState);

        _console.WriteHeader("Installation successful");
        _console.WriteSuccess($"Midnight Panel is online at: https://{domain}");
        _console.WriteInfo("Create an administrator account with: midnight create-admin");
        return ExitCodes.Success;
    }

    private static string GenerateRandomSecret(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..length];
    }
}
