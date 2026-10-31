using System.Diagnostics;
using System.Text;

namespace Midnight.Cli.Common;

public class ProcessRunner : IProcessRunner
{
    private readonly CliContext _context;
    private readonly IConsoleOutput _console;

    public ProcessRunner(CliContext context, IConsoleOutput console)
    {
        _context = context;
        _console = console;
    }

    public async Task<ProcessResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environment = null,
        bool isMutating = false,
        CancellationToken cancellationToken = default)
    {
        var sanitizedArgs = SecretScrubber.Redact(arguments);

        if (_context.DryRun && isMutating)
        {
            _console.WriteInfo($"[DRY-RUN] Würde Befehl ausführen: {fileName} {sanitizedArgs}");
            LogToFile($"[DRY-RUN] {fileName} {sanitizedArgs}");
            return new ProcessResult(0, "[DRY-RUN simulation]", string.Empty);
        }

        LogToFile($"[EXEC] {fileName} {sanitizedArgs} (Cwd: {workingDirectory ?? "inherit"})");

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (environment != null)
        {
            foreach (var (k, v) in environment)
            {
                psi.Environment[k] = v;
            }
        }

        try
        {
            using var process = new Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var stdoutStr = stdout.ToString();
            var stderrStr = stderr.ToString();

            LogToFile($"[EXIT {process.ExitCode}] {SecretScrubber.Redact(stdoutStr + stderrStr)}");

            return new ProcessResult(process.ExitCode, stdoutStr, stderrStr);
        }
        catch (Exception ex)
        {
            LogToFile($"[ERROR] Ausnahme bei Ausführung von {fileName}: {ex.Message}");
            return new ProcessResult(127, string.Empty, ex.Message);
        }
    }

    public Task<ProcessResult> RunBashAsync(
        string bashScript,
        string? workingDirectory = null,
        IDictionary<string, string>? environment = null,
        bool isMutating = false,
        CancellationToken cancellationToken = default)
    {
        return RunAsync("/bin/bash", $"-c \"{bashScript.Replace("\"", "\\\"")}\"", workingDirectory, environment, isMutating, cancellationToken);
    }

    private void LogToFile(string line)
    {
        try
        {
            var logPath = _context.LogFilePath;
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {SecretScrubber.Redact(line)}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // Logging-Fehler dürfen den Hauptablauf nicht blockieren
        }
    }
}
