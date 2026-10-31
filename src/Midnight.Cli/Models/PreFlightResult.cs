using Midnight.Cli.Common;

namespace Midnight.Cli.Models;

public record PortConflict(
    int Port,
    string ProcessName,
    int? Pid = null);

public record PreFlightResult(
    IReadOnlyList<CheckItem> Checks)
{
    public bool CanProceed => Checks.All(c => c.Status != CheckStatus.Failed);
    public bool HasWarnings => Checks.Any(c => c.Status == CheckStatus.Warning);
    public int ErrorCount => Checks.Count(c => c.Status == CheckStatus.Failed);
    public int WarningCount => Checks.Count(c => c.Status == CheckStatus.Warning);
}
