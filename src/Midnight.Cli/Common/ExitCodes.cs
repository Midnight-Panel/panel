namespace Midnight.Cli.Common;

/// <summary>
/// Common exit codes of the Midnight CLI.
/// </summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int GeneralError = 1;
    public const int PreFlightFailed = 2;
    public const int Cancelled = 3;
    public const int HealthCheckFailed = 4;
    public const int RollbackExecuted = 5;
    public const int InvalidArguments = 6;
}
