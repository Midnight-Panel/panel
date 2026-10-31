using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class ExtensionCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "extension";
    public string Description => "Manage extensions and plugins for the Midnight Panel";

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
        _console.WriteInfo("Fetching installed extensions...");

        var sql = "SELECT \"Identifier\", \"Name\", \"Category\", \"Version\", \"IsEnabled\" FROM \"Extensions\" ORDER BY \"Category\", \"Name\";";
        var res = await RunPostgresQueryAsync(sql, cancellationToken);

        if (!res.Success)
        {
            _console.WriteError($"Could not fetch extensions: {res.Output}");
            return ExitCodes.GeneralError;
        }

        if (isJson)
        {
            _console.WriteLine(res.Output);
            return ExitCodes.Success;
        }

        _console.WriteSuccess("Installed Midnight Panel extensions:");
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
            _console.WriteError($"Please specify an identifier: midnight extension {(isEnable ? "enable" : "disable")} <identifier>");
            return ExitCodes.InvalidArguments;
        }

        var statusText = isEnable ? "true" : "false";
        var verb = isEnable ? "enabled" : "disabled";

        _console.WriteInfo($"Setting extension '{identifier}' to {verb}...");
        var sql = $"UPDATE \"Extensions\" SET \"IsEnabled\" = {statusText}, \"UpdatedAt\" = NOW() WHERE LOWER(\"Identifier\") = LOWER('{identifier}');";

        var res = await RunPostgresQueryAsync(sql, cancellationToken);
        if (res.Success)
        {
            _console.WriteSuccess($"Extension '{identifier}' was {verb} successfully.");
            return ExitCodes.Success;
        }

        _console.WriteError($"Failed to change status: {res.Output}");
        return ExitCodes.GeneralError;
    }

    private async Task<int> InstallExtensionAsync(string[] args, CancellationToken cancellationToken)
    {
        var filePath = args.Skip(1).FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _console.WriteError($"Please specify a valid path to a .zip file: midnight extension install <path.zip>");
            return ExitCodes.InvalidArguments;
        }

        _console.WriteInfo($"Copying extension package '{filePath}' into the panel container...");
        var dest = $"/tmp/{Path.GetFileName(filePath)}";

        var cpRes = await _processRunner.RunAsync(
            "docker",
            $"compose -f /opt/midnight/panel/docker-compose.yml cp \"{filePath}\" panel:{dest}",
            isMutating: true,
            cancellationToken: cancellationToken);

        if (!cpRes.Success)
        {
            _console.WriteError($"Failed to copy package: {cpRes.Output}");
            return ExitCodes.GeneralError;
        }

        _console.WriteSuccess($"Package '{Path.GetFileName(filePath)}' staged. Import it via the web panel.");
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
        midnight extension - Manage extensions and plugins

        USAGE:
          midnight extension <subcommand> [options]

        COMMANDS:
          list [--json]              List all installed extensions
          enable <identifier>        Enable an extension
          disable <identifier>       Disable an extension
          install <archive.zip>      Copy and install an extension package
        """);
        return ExitCodes.Success;
    }
}
