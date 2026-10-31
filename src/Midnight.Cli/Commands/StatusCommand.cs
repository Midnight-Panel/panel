using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class StatusCommand : ICommand
{
    private readonly IStatusService _statusService;

    public string Name => "status";
    public string Description => "Show the status of the panel compose stack and the demon (--json)";

    public StatusCommand(IStatusService statusService)
    {
        _statusService = statusService;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var isJson = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));
        await _statusService.PrintStatusAsync(isJson, cancellationToken);
        return ExitCodes.Success;
    }
}
