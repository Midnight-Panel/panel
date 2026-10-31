using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Security;
using Midnight.Shared;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.Overview.Http;

[ApiController]
[Route("api/admin/overview")]
public class AdminOverviewController : ControllerBase
{
    private readonly DataContext _context;

    public AdminOverviewController(DataContext context)
    {
        _context = context;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminOverview)]
    public async Task<AdminOverviewResponse> Get(CancellationToken cancellationToken)
    {
        using var process = Process.GetCurrentProcess();

        return new AdminOverviewResponse
        {
            UserCount = await _context.Users.CountAsync(cancellationToken),
            LockedUserCount = await _context.Users.CountAsync(x => x.IsLocked, cancellationToken),
            NodeCount = await _context.Nodes.CountAsync(cancellationToken),
            ServerCount = await _context.Servers.CountAsync(cancellationToken),
            Version = typeof(AdminOverviewController).Assembly.GetName().Version?.ToString(3) ?? "",
            Uptime = DateTime.Now - process.StartTime,
            MemoryUsageBytes = process.WorkingSet64,
            OperatingSystem = RuntimeInformation.OSDescription,
            Runtime = RuntimeInformation.FrameworkDescription
        };
    }
}
