using Midnight.Cli.Common;

namespace Midnight.Cli.Commands;

public class StartCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "start";
    public string Description => "Start the panel (Docker Compose) or demon (systemd)";

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
            _console.WriteInfo("Starting panel Docker Compose stack...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml up -d", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Panel started.");
        }

        if (target is "demon" or "both")
        {
            _console.WriteInfo("Starting midnight-demon service...");
            await _processRunner.RunAsync("systemctl", "start midnight-demon", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Demon started.");
        }

        return ExitCodes.Success;
    }
}

public class StopCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "stop";
    public string Description => "Stop the panel or demon";

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
            _console.WriteInfo("Stopping panel Docker Compose stack...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml stop", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Panel stopped.");
        }

        if (target is "demon" or "both")
        {
            _console.WriteInfo("Stopping midnight-demon service...");
            await _processRunner.RunAsync("systemctl", "stop midnight-demon", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Demon stopped.");
        }

        return ExitCodes.Success;
    }
}

public class RestartCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "restart";
    public string Description => "Restart the panel or demon";

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
            _console.WriteInfo("Restarting panel Docker Compose stack...");
            await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml restart", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Panel restarted.");
        }

        if (target is "demon" or "both")
        {
            _console.WriteInfo("Restarting midnight-demon service...");
            await _processRunner.RunAsync("systemctl", "restart midnight-demon", isMutating: true, cancellationToken: cancellationToken);
            _console.WriteSuccess("Demon restarted.");
        }

        return ExitCodes.Success;
    }
}
