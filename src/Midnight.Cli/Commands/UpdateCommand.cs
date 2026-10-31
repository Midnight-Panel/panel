using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class UpdateCommand : ICommand
{
    private readonly IUpdateService _updateService;
    private readonly IConsoleOutput _console;

    public string Name => "update";
    public string Description => "Update panel or daemon with automatic backup, health check and rollback";

    public UpdateCommand(IUpdateService updateService, IConsoleOutput console)
    {
        _updateService = updateService;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var target = args.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? "panel";
        string? targetVersion = null;
        var useImages = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--version", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                targetVersion = args[++i];
            }
            else if (arg.Equals("--use-images", StringComparison.OrdinalIgnoreCase))
            {
                useImages = true;
            }
        }

        bool success;
        if (target == "daemon")
        {
            success = await _updateService.UpdateDaemonAsync(targetVersion, cancellationToken);
        }
        else
        {
            success = await _updateService.UpdatePanelAsync(targetVersion, useImages, cancellationToken);
        }

        return success ? ExitCodes.Success : ExitCodes.RollbackExecuted;
    }
}

public class RollbackCommand : ICommand
{
    private readonly IUpdateService _updateService;
    private readonly IConsoleOutput _console;

    public string Name => "rollback";
    public string Description => "Manually roll panel or daemon back to the previous version";

    public RollbackCommand(IUpdateService updateService, IConsoleOutput console)
    {
        _updateService = updateService;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var target = args.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? "panel";

        var confirmed = _console.Confirm(
            $"Really roll '{target}' back to the previous version?",
            defaultYes: false);

        if (!confirmed)
        {
            _console.WriteWarning("Rollback abgebrochen.");
            return ExitCodes.Cancelled;
        }

        var success = target == "daemon"
            ? await _updateService.RollbackDaemonAsync(cancellationToken)
            : await _updateService.RollbackPanelAsync(cancellationToken);

        return success ? ExitCodes.Success : ExitCodes.GeneralError;
    }
}
