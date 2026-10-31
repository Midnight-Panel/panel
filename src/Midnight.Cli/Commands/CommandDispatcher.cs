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
            _console.WriteError($"Unbekannter Befehl: '{commandName}'");
            _console.WriteLine("Führe 'midnight --help' aus, um alle verfügbaren Befehle anzuzeigen.");
            return ExitCodes.InvalidArguments;
        }

        // Argumente ohne den Befehlsnamen selbst filtern
        var cmdArgs = args.Where(a => !string.Equals(a, commandName, StringComparison.OrdinalIgnoreCase)).ToArray();

        try
        {
            return await command.ExecuteAsync(cmdArgs, cancellationToken);
        }
        catch (Exception ex)
        {
            _console.WriteError($"Kritischer Fehler bei der Ausführung von '{commandName}': {ex.Message}");
            _console.ShowLogTail(_context.LogFilePath, 20);
            return ExitCodes.GeneralError;
        }
    }

    private void PrintHelp()
    {
        Console.WriteLine("""
        midnight - Installations- und Verwaltungs-CLI für Midnight Panel & Daemon

        VERWENDUNG:
          midnight <befehl> [ziel] [optionen]

        KERN-BEFEHLE:
          install [panel|daemon|both]    Installiere Midnight Panel und/oder Daemon
          status [--json]                Zeige Status der Container und Dienste
          start [panel|daemon|both]      Starte Komponenten
          stop [panel|daemon|both]       Stoppe Komponenten
          restart [panel|daemon|both]    Starte Komponenten neu
          logs [target] [-f] [--tail N]  Zeige Live-Logs an (panel, db, caddy, daemon, installer)

        WARTUNG & UPDATES:
          update [target] [--use-images] Aktualisiere mit automatischem Backup & Auto-Rollback
          rollback [target]              Manueller Rollback auf die vorherige Version
          backup [dir] [--encrypt]       Erstelle vollständiges Backup (PostgreSQL, Storage, .env)
          restore <archiv>               Stelle System aus Backup-Archiv wieder her
          doctor                         Führe Systemdiagnose und Rechteprüfung durch
          ssl renew                      Erneuere SSL/TLS-Zertifikate in Caddy

        KONFIGURATION & VERWALTUNG:
          configure                      Interaktiver Konfigurations-Assistent
          create-admin [--email <mail>]  Erstelle einen neuen Administrator-Account
          completion [bash|zsh]          Generiere Shell-Autovervollständigung
          self-update                    Aktualisiere das midnight CLI-Binary
          version                        Zeige Versionsnummer an
          uninstall                      Deinstalliere Midnight vom System

        GLOBALE OPTIONEN:
          --dry-run                      Simuliere Aktionen ohne Änderungen am System
          -y, --yes                      Bestätige alle Abfragen automatisch
          --json                         Gebe Status und Daten als JSON aus
          -h, --help                     Zeige diese Hilfe an
        """);
    }
}
