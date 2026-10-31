using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class CreateAdminCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "create-admin";
    public string Description => "Erstelle einen neuen Administrator-Account im Panel";

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
            email = _console.Ask("E-Mail-Adresse für den Administrator", "admin@example.com");
        }

        var password = _console.Ask("Passwort für den Administrator", isPassword: true);
        if (string.IsNullOrEmpty(password))
        {
            _console.WriteError("Passwort darf nicht leer sein.");
            return ExitCodes.InvalidArguments;
        }

        _console.WriteInfo($"Erstelle Administrator-Account für {email}...");
        var env = new Dictionary<string, string> { ["MIDNIGHT_ADMIN_PASSWORD"] = password };

        var res = await _processRunner.RunAsync(
            "docker",
            $"compose -f /opt/midnight/panel/docker-compose.yml exec -T -e MIDNIGHT_ADMIN_PASSWORD=\"{password}\" panel dotnet Midnight.ApiServer.dll create-admin --email \"{email}\"",
            environment: env,
            isMutating: true,
            cancellationToken: cancellationToken);

        if (res.Success)
        {
            _console.WriteSuccess($"Administrator-Account {email} erfolgreich erstellt!");
            return ExitCodes.Success;
        }

        _console.WriteError($"Fehler bei der Kontoerstellung: {res.Output}");
        return ExitCodes.GeneralError;
    }
}

public class ConfigureCommand : ICommand
{
    private readonly IStateService _stateService;
    private readonly IFileSystem _fileSystem;
    private readonly IConsoleOutput _console;

    public string Name => "configure";
    public string Description => "Interaktiver Assistent zum Anpassen von Domain, Ports und Datenbank";

    public ConfigureCommand(IStateService stateService, IFileSystem fileSystem, IConsoleOutput console)
    {
        _stateService = stateService;
        _fileSystem = fileSystem;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var state = await _stateService.LoadAsync();
        _console.WriteHeader("Midnight Konfiguration");

        var newDomain = _console.Ask("Panel Domain", state.PanelDomain);
        var newPortStr = _console.Ask("Panel Port", state.PanelPort.ToString());
        int.TryParse(newPortStr, out var newPort);

        state.PanelDomain = newDomain;
        state.PanelPort = newPort > 0 ? newPort : 80;
        await _stateService.SaveAsync(state);

        // .env aktualisieren
        var envPath = "/opt/midnight/panel/.env";
        if (_fileSystem.FileExists(envPath))
        {
            var env = await _fileSystem.ReadAllTextAsync(envPath);
            env = System.Text.RegularExpressions.Regex.Replace(env, @"PANEL_DOMAIN=.*", $"PANEL_DOMAIN={newDomain}");
            await _fileSystem.WriteAllTextAsync(envPath, env, isSecret: true);
            await _fileSystem.SetPermissionsAsync(envPath, "600");
        }

        _console.WriteSuccess("Konfiguration erfolgreich aktualisiert.");
        _console.WriteInfo("Starte das Panel neu, um Änderungen anzuwenden: midnight restart panel");
        return ExitCodes.Success;
    }
}
