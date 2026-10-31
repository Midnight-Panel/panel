using Midnight.Cli.Common;
using Midnight.Cli.Services;

namespace Midnight.Cli.Commands;

public class DoctorCommand : ICommand
{
    private readonly IDoctorService _doctorService;

    public string Name => "doctor";
    public string Description => "Umfassende Diagnose des Systems, der Docker-Umgebung und sensibler Dateirechte";

    public DoctorCommand(IDoctorService doctorService)
    {
        _doctorService = doctorService;
    }

    public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var success = await _doctorService.RunDoctorAsync(cancellationToken);
        return success ? ExitCodes.Success : ExitCodes.GeneralError;
    }
}
