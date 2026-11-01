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

        // The identifier ends up in the SQL statement, so only the characters identifiers may contain are allowed
        if (!System.Text.RegularExpressions.Regex.IsMatch(identifier, "^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$"))
        {
            _console.WriteError($"'{identifier}' is not a valid extension identifier (letters, numbers, . _ -)");
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
        var target = args.Skip(1).FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(target))
        {
            _console.WriteError("Please specify a file path (.zip) or a download URL: midnight extension install <path-or-url>");
            return ExitCodes.InvalidArguments;
        }

        var isUrl = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (isUrl)
        {
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                target.IndexOfAny(['"', '\\', '`', '$', '\n', '\r']) >= 0)
            {
                _console.WriteError($"Invalid download URL '{target}'.");
                return ExitCodes.InvalidArguments;
            }

            _console.WriteInfo($"Downloading extension from '{target}'...");
            var curlRes = await _processRunner.RunAsync(
                "docker",
                $"compose -f /opt/midnight/panel/docker-compose.yml exec -T panel curl -fsSL -o /tmp/extension-download.zip \"{target}\"",
                isMutating: true,
                cancellationToken: cancellationToken);

            if (!curlRes.Success)
            {
                _console.WriteError($"Failed to download extension: {curlRes.Output}");
                return ExitCodes.GeneralError;
            }

            _console.WriteSuccess("Package downloaded. Enable it in the panel under Extensions.");
            return ExitCodes.Success;
        }

        if (!File.Exists(target))
        {
            _console.WriteError($"File '{target}' does not exist.");
            return ExitCodes.InvalidArguments;
        }

        _console.WriteInfo($"Copying extension package '{target}' into the panel container...");
        var dest = $"/tmp/{Path.GetFileName(target)}";

        var cpRes = await _processRunner.RunAsync(
            "docker",
            $"compose -f /opt/midnight/panel/docker-compose.yml cp \"{target}\" panel:{dest}",
            isMutating: true,
            cancellationToken: cancellationToken);

        if (!cpRes.Success)
        {
            _console.WriteError($"Failed to copy package: {cpRes.Output}");
            return ExitCodes.GeneralError;
        }

        _console.WriteSuccess($"Package '{Path.GetFileName(target)}' copied to the panel container.");
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
          install <archive.zip|url> Copy or download an extension package
        """);
        return ExitCodes.Success;
    }
}
