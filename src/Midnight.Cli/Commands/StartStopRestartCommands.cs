using Midnight.Cli.Common;

namespace Midnight.Cli.Commands;

public class StartCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "start";
    public string Description => "Starte Panel (Docker Compose) oder Daemon (systemd)";

    public StartCommand(IProcessRunner processRunner, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var target = args.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? "both";

        if (target is "panel" or "both")
        {
            _console.WriteInfo("Starte Panel Docker-Compose-Stack...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml up -d", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Panel gestartet.");
        }

        if (target is "daemon" or "both")
        {
            _console.WriteInfo("Starte midnight-daemon Dienst...");
            await _processRunner.RunAsync("systemctl", "start midnight-daemon", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Daemon gestartet.");
        }

        return ExitCodes.Success;
    }
}

public class StopCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "stop";
    public string Description => "Stoppe Panel oder Daemon";

    public StopCommand(IProcessRunner processRunner, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var target = args.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? "both";

        if (target is "panel" or "both")
        {
            _console.WriteInfo("Stoppe Panel Docker-Compose-Stack...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml stop", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Panel gestoppt.");
        }

        if (target is "daemon" or "both")
        {
            _console.WriteInfo("Stoppe midnight-daemon Dienst...");
            await _processRunner.RunAsync("systemctl", "stop midnight-daemon", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Daemon gestoppt.");
        }

        return ExitCodes.Success;
    }
}

public class RestartCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "restart";
    public string Description => "Starte Panel oder Daemon neu";

    public RestartCommand(IProcessRunner processRunner, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var target = args.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? "both";

        if (target is "panel" or "both")
        {
            _console.WriteInfo("Starte Panel Docker-Compose-Stack neu...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml restart", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Panel neu gestartet.");
        }

        if (target is "daemon" or "both")
        {
            _console.WriteInfo("Starte midnight-daemon Dienst neu...");
            await _processRunner.RunAsync("systemctl", "restart midnight-daemon", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Daemon neu gestartet.");
        }

        return ExitCodes.Success;
    }
}
