namespace Midnight.Cli.Common;

public enum CheckStatus
{
    Ok,
    Warning,
    Failed
}

public record CheckItem(
    string Name,
    CheckStatus Status,
    string Details,
    string? Remediation = null);

public interface IConsoleOutput
{
    bool IsJsonMode { get; set; }
    void Write(string message);
    void WriteLine(string message = "");
    void WriteInfo(string message);
    void WriteSuccess(string message);
    void WriteWarning(string message);
    void WriteError(string message);
    void WriteHeader(string title);
    void WriteCheckItem(CheckItem item);
    void WriteChecklist(string title, IEnumerable<CheckItem> items);
    bool Confirm(string prompt, bool defaultYes = true);
    int SelectOption(string prompt, IReadOnlyList<string> options, int defaultIndex = 0);
    string Ask(string prompt, string? defaultValue = null, bool isPassword = false);
    void ShowLogTail(string logFilePath, int lines = 25);
}
