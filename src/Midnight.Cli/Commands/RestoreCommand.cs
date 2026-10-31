using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class RestoreCommand : ICommand
{
    private readonly IRestoreService _restoreService;
    private readonly IConsoleOutput _console;

    public string Name => "restore";
    public string Description => "Stelle Midnight Panel und Datenbank aus einem Backup-Archiv wieder her";

    public RestoreCommand(IRestoreService restoreService, IConsoleOutput console)
    {
        _restoreService = restoreService;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var archivePath = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            _console.WriteError("Bitte Pfad zum Backup-Archiv angeben: midnight restore <archiv.tar.gz>");
            return ExitCodes.InvalidArguments;
        }

        var success = await _restoreService.RestoreAsync(archivePath, cancellationToken: cancellationToken);
        return success ? ExitCodes.Success : ExitCodes.GeneralError;
    }
}
