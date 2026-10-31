using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class CreateAdminCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "create-admin";
    public string Description => "Create a new administrator account in the panel";

    public CreateAdminCommand(IProcessRunner processRunner, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        string? email = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--email", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                email = args[++i];
            }
        }

        if (string.IsNullOrEmpty(email))
        {
            email = _console.Ask("Email address for the administrator", "admin@example.com");
        }

        // The email is passed as process argument, quotes or whitespace would break out of it
        if (string.IsNullOrWhiteSpace(email) || email.Any(c => char.IsWhiteSpace(c) || c is '"' or '\'' or '\\' or '`' or '$') || !email.Contains('@'))
        {
            _console.WriteError("Invalid email address.");
            return ExitCodes.InvalidArguments;
        }

        var password = _console.Ask("Password for the administrator", isPassword: true);
        if (string.IsNullOrEmpty(password))
        {
            _console.WriteError("Password must not be empty.");
            return ExitCodes.InvalidArguments;
        }

        _console.WriteInfo($"Creating administrator account for {email}...");
        var env = new Dictionary<string, string> { ["MIDNIGHT_ADMIN_PASSWORD"] = password };

        var res = await _processRunner.RunAsync(
            "docker",
            // "-e NAME" without value passes the variable from the environment below, so the password
            // never appears in the process list or the log file
            $"compose -f /opt/midnight/panel/docker-compose.yml exec -T -e MIDNIGHT_ADMIN_PASSWORD panel dotnet Midnight.ApiServer.dll create-admin --email \"{email}\"",
            environment: env,
            isMutating: true,
            cancellationToken: cancellationToken);

        if (res.Success)
        {
            _console.WriteSuccess($"Administrator account {email} created successfully!");
            return ExitCodes.Success;
        }

        _console.WriteError($"Account creation failed: {res.Output}");
        return ExitCodes.GeneralError;
    }
}

public class ConfigureCommand : ICommand
{
    private readonly IStateService _stateService;
    private readonly IFileSystem _fileSystem;
    private readonly IConsoleOutput _console;

    public string Name => "configure";
    public string Description => "Interactive wizard for changing domain, ports and database";

    public ConfigureCommand(IStateService stateService, IFileSystem fileSystem, IConsoleOutput console)
    {
        _stateService = stateService;
        _fileSystem = fileSystem;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var state = await _stateService.LoadAsync();
        _console.WriteHeader("Midnight Configuration");

        var newDomain = _console.Ask("Panel Domain", state.PanelDomain);
        var newPortStr = _console.Ask("Panel Port", state.PanelPort.ToString());
        int.TryParse(newPortStr, out var newPort);

        state.PanelDomain = newDomain;
        state.PanelPort = newPort > 0 ? newPort : 80;
        await _stateService.SaveAsync(state);

        // Update .env
        var envPath = "/opt/midnight/panel/.env";
        if (_fileSystem.FileExists(envPath))
        {
            var env = await _fileSystem.ReadAllTextAsync(envPath);
            env = System.Text.RegularExpressions.Regex.Replace(env, @"PANEL_DOMAIN=.*", $"PANEL_DOMAIN={newDomain}");
            await _fileSystem.WriteAllTextAsync(envPath, env, isSecret: true);
            await _fileSystem.SetPermissionsAsync(envPath, "600");
        }

        _console.WriteSuccess("Configuration updated successfully.");
        _console.WriteInfo("Restart the panel to apply changes: midnight restart panel");
        return ExitCodes.Success;
    }
}
