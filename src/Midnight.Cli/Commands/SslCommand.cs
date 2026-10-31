using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class SslCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "ssl";
    public string Description => "Verwalte SSL/TLS-Zertifikate (ssl renew)";

    public SslCommand(IProcessRunner processRunner, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var action = args.FirstOrDefault()?.ToLowerInvariant() ?? "renew";

        if (action == "renew")
        {
            _console.WriteInfo("Erneuere Caddy SSL-Zertifikate...");
            var res = await _processRunner.RunAsync(
                "docker",
                "compose -f /opt/midnight/panel/docker-compose.yml exec -T caddy caddy reload",
                isMutating: true,
                cancellationToken: cancellationToken);

            if (res.Success)
            {
                _console.WriteSuccess("Caddy-Zertifikate erfolgreich neu geladen!");
                return ExitCodes.Success;
            }

            _console.WriteError($"Fehler beim Neuladen der SSL-Zertifikate: {res.Output}");
            return ExitCodes.GeneralError;
        }

        _console.WriteError($"Unbekannte SSL-Aktion '{action}'. Verfügbar: renew");
        return ExitCodes.InvalidArguments;
    }
}

public class CompletionCommand : ICommand
{
    private readonly ICompletionService _completionService;

    public string Name => "completion";
    public string Description => "Generiere Shell-Completion Skripte (bash, zsh)";

    public CompletionCommand(ICompletionService completionService)
    {
        _completionService = completionService;
    }

    public Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var shell = args.FirstOrDefault()?.ToLowerInvariant() ?? "bash";

        switch (shell)
        {
            case "bash":
                Console.WriteLine(_completionService.GenerateBashCompletion());
                return Task.FromResult(ExitCodes.Success);
            case "zsh":
                Console.WriteLine(_completionService.GenerateZshCompletion());
                return Task.FromResult(ExitCodes.Success);
            default:
                Console.Error.WriteLine($"Nicht unterstützte Shell: {shell}. Verfügbar: bash, zsh");
                return Task.FromResult(ExitCodes.InvalidArguments);
        }
    }
}

public class UninstallCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;
    private readonly IConsoleOutput _console;

    public string Name => "uninstall";
    public string Description => "Entferne Midnight Panel und/oder Daemon vom System";

    public UninstallCommand(IProcessRunner processRunner, IFileSystem fileSystem, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var confirmed = _console.Confirm(
            "ACHTUNG: Möchtest du Midnight Panel und alle zugehörigen Daten wirklich vollständig deinstallieren?",
            defaultYes: false);

        if (!confirmed)
        {
            _console.WriteWarning("Deinstallation abgebrochen.");
            return ExitCodes.Cancelled;
        }

        _console.WriteInfo("Stoppe und entferne Docker-Container...");
        await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml down -v", isMutating: true, cancellationToken: cancellationToken);

        _console.WriteInfo("Entferne systemd-Dienst...");
        await _processRunner.RunAsync("systemctl", "disable --now midnight-daemon", isMutating: true, cancellationToken: cancellationToken);
        _fileSystem.DeleteFile("/etc/systemd/system/midnight-daemon.service");

        _console.WriteInfo("Entferne Konfigurations- und Datenverzeichnisse...");
        _fileSystem.DeleteDirectory("/opt/midnight", recursive: true);
        _fileSystem.DeleteDirectory("/etc/midnight", recursive: true);

        _console.WriteSuccess("Midnight Panel wurde erfolgreich deinstalliert.");
        return ExitCodes.Success;
    }
}

public class VersionCommand : ICommand
{
    public string Name => "version";
    public string Description => "Zeige Version des Midnight CLI Tools";

    public Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("midnight CLI v0.1.0 (Midnight-Panel)");
        return Task.FromResult(ExitCodes.Success);
    }
}

public class SelfUpdateCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "self-update";
    public string Description => "Aktualisiere das midnight CLI Tool auf die neueste Version";

    public SelfUpdateCommand(IProcessRunner processRunner, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        _console.WriteInfo("Prüfe auf neuere Version von midnight CLI...");
        var script = "curl -fsSL https://raw.githubusercontent.com/Midnight-Panel/installer/main/install.sh | bash -s -- --self-update";
        var res = await _processRunner.RunBashAsync(script, isMutating: true, cancellationToken: cancellationToken);

        if (res.Success)
        {
            _console.WriteSuccess("midnight CLI erfolgreich aktualisiert!");
            return ExitCodes.Success;
        }

        _console.WriteError("Fehler beim Selbstupdate.");
        return ExitCodes.GeneralError;
    }
}
