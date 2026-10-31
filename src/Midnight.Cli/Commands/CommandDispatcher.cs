using Midnight.Cli.Common;

namespace Midnight.Cli.Commands;

public class CommandDispatcher
{
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly IConsoleOutput _console;
    private readonly CliContext _context;

    public CommandDispatcher(IEnumerable<ICommand> commands, IConsoleOutput console, CliContext context)
    {
        _console = console;
        _context = context;
        foreach (var cmd in commands)
        {
            _commands[cmd.Name] = cmd;
        }
    }

    public async Task<int> DispatchAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var commandName = args.FirstOrDefault(a => !a.StartsWith('-'));

        if (string.IsNullOrEmpty(commandName) || args.Any(a => a is "--help" or "-h"))
        {
            PrintHelp();
            return ExitCodes.Success;
        }

        if (!_commands.TryGetValue(commandName, out var command))
        {
            _console.WriteError($"Unknown command: '{commandName}'");
            _console.WriteLine("Run 'midnight --help' to see all available commands.");
            return ExitCodes.InvalidArguments;
        }

        // Filter the arguments, excluding the command name itself
        var cmdArgs = args.Where(a => !string.Equals(a, commandName, StringComparison.OrdinalIgnoreCase)).ToArray();

        try
        {
            return await command.ExecuteAsync(cmdArgs, cancellationToken);
        }
        catch (Exception ex)
        {
            _console.WriteError($"Critical error while running '{commandName}': {ex.Message}");
            _console.ShowLogTail(_context.LogFilePath, 20);
            return ExitCodes.GeneralError;
        }
    }

    private void PrintHelp()
    {
        Console.WriteLine("""
        midnight - Installation and management CLI for Midnight Panel & Daemon

        USAGE:
          midnight <command> [target] [options]

        CORE COMMANDS:
          install [panel|daemon|both]    Install Midnight Panel and/or Daemon
          status [--json]                Show status of containers and services
          start [panel|daemon|both]      Start components
          stop [panel|daemon|both]       Stop components
          restart [panel|daemon|both]    Restart components
          logs [target] [-f] [--tail N]  Show live logs (panel, db, caddy, daemon, installer)

        MAINTENANCE & UPDATES:
          update [target] [--use-images] Update with automatic backup & auto-rollback
          rollback [target]              Manual rollback to the previous version
          backup [dir] [--encrypt]       Create a full backup (PostgreSQL, storage, .env)
          restore <archive>              Restore the system from a backup archive
          doctor                         Run system diagnostics and permission checks
          ssl renew                      Renew SSL/TLS certificates in Caddy

        CONFIGURATION & MANAGEMENT:
          configure                      Interactive configuration wizard
          create-admin [--email <mail>]  Create a new administrator account
          completion [bash|zsh]          Generate shell completion
          self-update                    Update the midnight CLI binary
          version                        Show version number
          uninstall                      Uninstall Midnight from the system

        GLOBAL OPTIONS:
          --dry-run                      Simulate actions without changing the system
          -y, --yes                      Automatically confirm all prompts
          --json                         Output status and data as JSON
          -h, --help                     Show this help
        """);
    }
}
