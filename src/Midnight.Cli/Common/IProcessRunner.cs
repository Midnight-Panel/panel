namespace Midnight.Cli.Common;

public record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError)
{
    public bool Success => ExitCode == 0;
    public string Output => string.IsNullOrWhiteSpace(StandardOutput) ? StandardError : StandardOutput;
}

/// <summary>
/// Abstraktion für die Prozessausführung. Erlaubt zentrales Logging,
/// Redaction von Secrets, Mocking in Tests und --dry-run-Unterstützung.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environment = null,
        bool isMutating = false,
        CancellationToken cancellationToken = default);

    Task<ProcessResult> RunBashAsync(
        string bashScript,
        string? workingDirectory = null,
        IDictionary<string, string>? environment = null,
        bool isMutating = false,
        CancellationToken cancellationToken = default);
}
