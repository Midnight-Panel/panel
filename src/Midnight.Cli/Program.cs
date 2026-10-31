using Midnight.Cli.Commands;
using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Globale Flags parsen
        var isDryRun = args.Any(a => a.Equals("--dry-run", StringComparison.OrdinalIgnoreCase));
        var isYes = args.Any(a => a is "-y" or "--yes");
        var isDebug = args.Any(a => a.Equals("--debug", StringComparison.OrdinalIgnoreCase));

        var context = new CliContext
        {
            DryRun = isDryRun,
            Yes = isYes,
            Debug = isDebug
        };

        var console = new ConsoleOutput(context);
        var processRunner = new ProcessRunner(context, console);
        var fileSystem = new FileSystem(context, console);
        var stateService = new StateService(fileSystem, context);

        var preFlightService = new PreFlightService(processRunner, fileSystem, console, context);
        var backupService = new BackupService(processRunner, fileSystem, stateService, console, context);
        var restoreService = new RestoreService(processRunner, fileSystem, backupService, stateService, console, context);
        var updateService = new UpdateService(processRunner, fileSystem, backupService, restoreService, stateService, console, context);
        var statusService = new StatusService(processRunner, fileSystem, stateService, console);
        var doctorService = new DoctorService(preFlightService, fileSystem, processRunner, console, context);
        var completionService = new CompletionService();

        var commands = new ICommand[]
        {
            new InstallCommand(preFlightService, stateService, fileSystem, processRunner, console, context),
            new StatusCommand(statusService),
            new StartCommand(processRunner, console),
            new StopCommand(processRunner, console),
            new RestartCommand(processRunner, console),
            new LogsCommand(processRunner, context),
            new UpdateCommand(updateService, console),
            new RollbackCommand(updateService, console),
            new BackupCommand(backupService, console),
            new RestoreCommand(restoreService, console),
            new DoctorCommand(doctorService),
            new ConfigureCommand(stateService, fileSystem, console),
            new CreateAdminCommand(processRunner, console),
            new SslCommand(processRunner, console),
            new CompletionCommand(completionService),
            new UninstallCommand(processRunner, fileSystem, console),
            new VersionCommand(),
            new SelfUpdateCommand(processRunner, console)
        };

        var dispatcher = new CommandDispatcher(commands, console, context);

        // Sauberes Abfangen von STRG+C / SIGINT
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            console.WriteWarning("\nVorgang durch Benutzer abgebrochen.");
        };

        try
        {
            return await dispatcher.DispatchAsync(args, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.Cancelled;
        }
    }
}
