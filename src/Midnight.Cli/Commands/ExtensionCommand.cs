using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class ExtensionCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "extension";
    public string Description => "Verwalte Erweiterungen und Plugins für das Midnight Panel";

    public ExtensionCommand(IProcessRunner processRunner, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var action = args.FirstOrDefault(a => !a.StartsWith('-'))?.ToLowerInvariant() ?? "list";

        return action switch
        {
            "list" or "ls" => await ListExtensionsAsync(args, cancellationToken),
            "enable" => await ToggleExtensionAsync(args, isEnable: true, cancellationToken),
            "disable" => await ToggleExtensionAsync(args, isEnable: false, cancellationToken),
            "install" => await InstallExtensionAsync(args, cancellationToken),
            _ => PrintHelp()
        };
    }

    private async Task<int> ListExtensionsAsync(string[] args, CancellationToken cancellationToken)
    {
        var isJson = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));
        _console.WriteInfo("Rufe installierte Erweiterungen ab...");

        var sql = "SELECT \"Identifier\", \"Name\", \"Category\", \"Version\", \"IsEnabled\" FROM \"Extensions\" ORDER BY \"Category\", \"Name\";";
        var res = await RunPostgresQueryAsync(sql, cancellationToken);

        if (!res.Success)
        {
            _console.WriteError($"Konnte Erweiterungen nicht abrufen: {res.Output}");
            return ExitCodes.GeneralError;
        }

        if (isJson)
        {
            _console.WriteLine(res.Output);
            return ExitCodes.Success;
        }

        _console.WriteSuccess("Installierte Midnight Panel Erweiterungen:");
        _console.WriteLine("----------------------------------------------------------------------");
        _console.WriteLine(res.Output.Trim());
        _console.WriteLine("----------------------------------------------------------------------");
        return ExitCodes.Success;
    }

    private async Task<int> ToggleExtensionAsync(string[] args, bool isEnable, CancellationToken cancellationToken)
    {
        var identifier = args.Skip(1).FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(identifier))
        {
            _console.WriteError($"Bitte gib einen Identifier an: midnight extension {(isEnable ? "enable" : "disable")} <identifier>");
            return ExitCodes.InvalidArguments;
        }

        var statusText = isEnable ? "true" : "false";
        var verb = isEnable ? "aktiviert" : "deaktiviert";

        _console.WriteInfo($"Setze Erweiterung '{identifier}' auf {verb}...");
        var sql = $"UPDATE \"Extensions\" SET \"IsEnabled\" = {statusText}, \"UpdatedAt\" = NOW() WHERE LOWER(\"Identifier\") = LOWER('{identifier}');";

        var res = await RunPostgresQueryAsync(sql, cancellationToken);
        if (res.Success)
        {
            _console.WriteSuccess($"Erweiterung '{identifier}' wurde erfolgreich {verb}.");
            return ExitCodes.Success;
        }

        _console.WriteError($"Fehler beim Ändern des Status: {res.Output}");
        return ExitCodes.GeneralError;
    }

    private async Task<int> InstallExtensionAsync(string[] args, CancellationToken cancellationToken)
    {
        var filePath = args.Skip(1).FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _console.WriteError($"Bitte gib einen gültigen Pfad zu einer .zip-Datei an: midnight extension install <pfad.zip>");
            return ExitCodes.InvalidArguments;
        }

        _console.WriteInfo($"Kopiere Erweiterungspaket '{filePath}' in den Panel-Container...");
        var dest = $"/tmp/{Path.GetFileName(filePath)}";

        var cpRes = await _processRunner.RunAsync(
            "docker",
            $"compose -f /opt/midnight/panel/docker-compose.yml cp \"{filePath}\" panel:{dest}",
            isMutating: true,
            cancellationToken: cancellationToken);

        if (!cpRes.Success)
        {
            _console.WriteError($"Fehler beim Kopieren des Pakets: {cpRes.Output}");
            return ExitCodes.GeneralError;
        }

        _console.WriteSuccess($"Paket '{Path.GetFileName(filePath)}' bereitgestellt. Importiere über das Web-Panel.");
        return ExitCodes.Success;
    }

    private async Task<ProcessResult> RunPostgresQueryAsync(string sqlQuery, CancellationToken cancellationToken)
    {
        var escaped = sqlQuery.Replace("\"", "\\\"");
        return await _processRunner.RunAsync(
            "docker",
            $"compose -f /opt/midnight/panel/docker-compose.yml exec -T db psql -U midnight -d midnight -c \"{escaped}\"",
            cancellationToken: cancellationToken);
    }

    private int PrintHelp()
    {
        _console.WriteLine("""
        midnight extension - Verwalte Erweiterungen und Plugins

        VERWENDUNG:
          midnight extension <subcommand> [optionen]

        BEFEHLE:
          list [--json]              Liste alle installierten Erweiterungen auf
          enable <identifier>        Aktiviere eine Erweiterung
          disable <identifier>       Deaktiviere eine Erweiterung
          install <archiv.zip>       Kopiere und installiere ein Erweiterungspaket
        """);
        return ExitCodes.Success;
    }
}
