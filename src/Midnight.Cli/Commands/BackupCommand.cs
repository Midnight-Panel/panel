using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class BackupCommand : ICommand
{
    private readonly IBackupService _backupService;
    private readonly IConsoleOutput _console;

    public string Name => "backup";
    public string Description => "Erstelle ein vollständiges Backup von Datenbank, Storage und Konfiguration (--encrypt, --keep N)";

    public BackupCommand(IBackupService backupService, IConsoleOutput console)
    {
        _backupService = backupService;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        string? targetDir = null;
        var encrypt = false;
        int? keepCount = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--encrypt", StringComparison.OrdinalIgnoreCase))
            {
                encrypt = true;
            }
            else if (arg.Equals("--keep", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var k))
                {
                    keepCount = k;
                }
            }
            else if (!arg.StartsWith('-') && targetDir == null)
            {
                targetDir = arg;
            }
        }

        try
        {
            var archivePath = await _backupService.CreateBackupAsync(
                targetDir,
                encrypt,
                keepCount: keepCount,
                cancellationToken: cancellationToken);

            return ExitCodes.Success;
        }
        catch (Exception ex)
        {
            _console.WriteError($"Fehler beim Erstellen des Backups: {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }
}
