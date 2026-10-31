using Midnight.Cli.Common;

namespace Midnight.Cli.Tests.Mocks;

public class MockProcessRunner : IProcessRunner
{
    public List<(string FileName, string Arguments, bool IsMutating)> ExecutedCommands { get; } = [];
    public Dictionary<string, ProcessResult> CustomResults { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Func<string, string, ProcessResult>? CustomHandler { get; set; }

    public Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environment = null,
        bool isMutating = false,
        CancellationToken cancellationToken = default)
    {
        ExecutedCommands.Add((fileName, arguments, isMutating));

        if (CustomHandler != null)
        {
            return Task.FromResult(CustomHandler(fileName, arguments));
        }

        var key = $"{fileName} {arguments}".Trim();
        foreach (var (k, v) in CustomResults)
        {
            if (key.Contains(k, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(v);
            }
        }

        return Task.FromResult(new ProcessResult(0, "mock output", string.Empty));
    }

    public Task<ProcessResult> RunBashAsync(
        string bashScript,
        string? workingDirectory = null,
        IDictionary<string, string>? environment = null,
        bool isMutating = false,
        CancellationToken cancellationToken = default)
    {
        return RunAsync("/bin/bash", bashScript, workingDirectory, environment, isMutating, cancellationToken);
    }
}
