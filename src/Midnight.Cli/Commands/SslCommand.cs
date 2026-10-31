using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class SslCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "ssl";
    public string Description => "Manage SSL/TLS certificates (ssl renew)";

    public SslCommand(IProcessRunner processRunner, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var action = args.FirstOrDefault()?.ToLowerInvariant() ?? "renew";

        if (action == "renew")
        {
            _console.WriteInfo("Renewing Caddy SSL certificates...");
            var res = await _processRunner.RunAsync(
                "docker",
                "compose -f /opt/midnight/panel/docker-compose.yml exec -T caddy caddy reload",
                isMutating: true,
                cancellationToken: cancellationToken);

            if (res.Success)
            {
                _console.WriteSuccess("Caddy certificates reloaded successfully!");
                return ExitCodes.Success;
            }

            _console.WriteError($"Failed to reload SSL certificates: {res.Output}");
            return ExitCodes.GeneralError;
        }

        _console.WriteError($"Unknown SSL action '{action}'. Available: renew");
        return ExitCodes.InvalidArguments;
    }
}

public class CompletionCommand : ICommand
{
    private readonly ICompletionService _completionService;

    public string Name => "completion";
    public string Description => "Generate shell completion scripts (bash, zsh)";

    public CompletionCommand(ICompletionService completionService)
    {
        _completionService = completionService;
    }

    public Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var shell = args.FirstOrDefault()?.ToLowerInvariant() ?? "bash";

        switch (shell)
        {
            case "bash":
                Console.WriteLine(_completionService.GenerateBashCompletion());
                return Task.FromResult(ExitCodes.Success);
            case "zsh":
                Console.WriteLine(_completionService.GenerateZshCompletion());
                return Task.FromResult(ExitCodes.Success);
            default:
                Console.Error.WriteLine($"Unsupported shell: {shell}. Available: bash, zsh");
                return Task.FromResult(ExitCodes.InvalidArguments);
        }
    }
}

public class UninstallCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IFileSystem _fileSystem;
    private readonly IConsoleOutput _console;

    public string Name => "uninstall";
    public string Description => "Remove Midnight Panel and/or Daemon from the system";

    public UninstallCommand(IProcessRunner processRunner, IFileSystem fileSystem, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _fileSystem = fileSystem;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var confirmed = _console.Confirm(
            "WARNING: Do you really want to completely uninstall Midnight Panel and all associated data?",
            defaultYes: false);

        if (!confirmed)
        {
            _console.WriteWarning("Uninstall cancelled.");
            return ExitCodes.Cancelled;
        }

        _console.WriteInfo("Stopping and removing Docker containers...");
        await _processRunner.RunAsync("docker", "compose -f /opt/midnight/panel/docker-compose.yml down -v", isMutating: true, cancellationToken: cancellationToken);

        _console.WriteInfo("Removing systemd service...");
        await _processRunner.RunAsync("systemctl", "disable --now midnight-daemon", isMutating: true, cancellationToken: cancellationToken);
        _fileSystem.DeleteFile("/etc/systemd/system/midnight-daemon.service");

        _console.WriteInfo("Removing configuration and data directories...");
        _fileSystem.DeleteDirectory("/opt/midnight", recursive: true);
        _fileSystem.DeleteDirectory("/etc/midnight", recursive: true);

        _console.WriteSuccess("Midnight Panel was uninstalled successfully.");
        return ExitCodes.Success;
    }
}

public class VersionCommand : ICommand
{
    public string Name => "version";
    public string Description => "Show the Midnight CLI tool version";

    public Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("midnight CLI v0.1.0 (Midnight-Panel)");
        return Task.FromResult(ExitCodes.Success);
    }
}

public class SelfUpdateCommand : ICommand
{
    private readonly IProcessRunner _processRunner;
    private readonly IConsoleOutput _console;

    public string Name => "self-update";
    public string Description => "Update the midnight CLI tool to the latest version";

    public SelfUpdateCommand(IProcessRunner processRunner, IConsoleOutput console)
    {
        _processRunner = processRunner;
        _console = console;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        _console.WriteInfo("Checking for a newer version of midnight CLI...");
        var script = "curl -fsSL https://raw.githubusercontent.com/Midnight-Panel/installer/main/install.sh | bash -s -- --self-update";
        var res = await _processRunner.RunBashAsync(script, isMutating: true, cancellationToken: cancellationToken);

        if (res.Success)
        {
            _console.WriteSuccess("midnight CLI updated successfully!");
            return ExitCodes.Success;
        }

        _console.WriteError("Self-update failed.");
        return ExitCodes.GeneralError;
    }
}
