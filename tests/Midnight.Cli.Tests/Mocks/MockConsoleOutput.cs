using Midnight.Cli.Common;

namespace Midnight.Cli.Tests.Mocks;

public class MockConsoleOutput : IConsoleOutput
{
    public bool IsJsonMode { get; set; }
    public List<string> Messages { get; } = [];
    public List<CheckItem> CheckedItems { get; } = [];
    public bool ConfirmResponse { get; set; } = true;
    public int SelectOptionResponse { get; set; } = 0;
    public string AskResponse { get; set; } = "mock";

    public void Write(string message) => Messages.Add(message);
    public void WriteLine(string message = "") => Messages.Add(message);
    public void WriteInfo(string message) => Messages.Add($"[INFO] {message}");
    public void WriteSuccess(string message) => Messages.Add($"[SUCCESS] {message}");
    public void WriteWarning(string message) => Messages.Add($"[WARNING] {message}");
    public void WriteError(string message) => Messages.Add($"[ERROR] {message}");
    public void WriteHeader(string title) => Messages.Add($"=== {title} ===");
    public void WriteCheckItem(CheckItem item) => CheckedItems.Add(item);

    public void WriteChecklist(string title, IEnumerable<CheckItem> items)
    {
        Messages.Add($"=== {title} ===");
        CheckedItems.AddRange(items);
    }

    public bool Confirm(string prompt, bool defaultYes = true) => ConfirmResponse;
    public int SelectOption(string prompt, IReadOnlyList<string> options, int defaultIndex = 0) => SelectOptionResponse;
    public string Ask(string prompt, string? defaultValue = null, bool isPassword = false) => AskResponse;
    public void ShowLogTail(string logFilePath, int lines = 25) { }
}
