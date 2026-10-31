using Midnight.Cli.Common;

namespace Midnight.Cli.Commands;

public class LogsCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly CliContext _context;

    public string Name => "logs";
    public string Description => "Zeige Container- oder Daemon-Logs an (-f, --tail)";

    public LogsCommand(IProcessRunner processRunner, CliContext context)
    {
        _processRunner = processRunner;
        _context = context;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var target = args.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? "panel";
        var follow = args.Any(a => a is "-f" or "--follow");
        var tail = "100";

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--tail", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                tail = args[++i];
            }
        }

        if (target == "installer")
        {
            var followArg = follow ? "-f" : "";
            await _processRunner.RunAsync("tail", $"-n {tail} {followArg} {_context.LogFilePath}", cancellationToken: cancellationToken);
            return ExitCodes.Success;
        }

        if (target == "daemon")
        {
            var followArg = follow ? "-f" : "";
            await _processRunner.RunAsync("journalctl", $"-u midnight-daemon -n {tail} {followArg}", cancellationToken: cancellationToken);
            return ExitCodes.Success;
        }

        // Docker compose logs of the panel
        var fArg = follow ? "-f" : "";
        var service = target is "db" or "caddy" ? target : "";
        await _processRunner.RunAsync("docker", $"compose -f /opt/midnight/panel/docker-compose.yml logs --tail {tail} {fArg} {service}".Trim(), cancellationToken: cancellationToken);
        return ExitCodes.Success;
    }
}
