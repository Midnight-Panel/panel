using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Users.Services;
using Midnight.Shared;
using Midnight.Shared.Http.Admin;

namespace Midnight.ApiServer.Features.Users.Http;

[ApiController]
[Route("api/admin/roles")]
public class AdminRolesController : ControllerBase
{
    private readonly RoleService _roleService;
    private readonly ILogger<AdminRolesController> _logger;

    public AdminRolesController(RoleService roleService, ILogger<AdminRolesController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    [HttpGet]
    [RequirePermission(Permissions.AdminRolesRead)]
    public Task<List<RoleResponse>> List(CancellationToken cancellationToken)
        => _roleService.ListAsync(cancellationToken);

    /// <summary>All permissions known to the panel, grouped for the editor</summary>
    [HttpGet("permissions")]
    [RequirePermission(Permissions.AdminRolesRead)]
    public PermissionGroup[] ListPermissions() => Permissions.Groups;

    [HttpPost]
    [RequirePermission(Permissions.AdminRolesWrite)]
    public async Task<RoleResponse> Create([FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleService.CreateAsync(request, User.GetPermissions().ToList(), cancellationToken);
        _logger.LogInformation("User {ActorId} created role {RoleId}", User.GetUserId(), role.Id);
        return role;
    }

    [HttpPut("{id:int}")]
    [RequirePermission(Permissions.AdminRolesWrite)]
    public async Task<RoleResponse> Update(int id, [FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        var role = await _roleService.UpdateAsync(id, request, User.GetPermissions().ToList(), cancellationToken);
        _logger.LogInformation("User {ActorId} updated role {RoleId}", User.GetUserId(), role.Id);
        return role;
    }

    [HttpDelete("{id:int}")]
    [RequirePermission(Permissions.AdminRolesWrite)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _roleService.DeleteAsync(id, User.GetPermissions().ToList(), cancellationToken);
        _logger.LogInformation("User {ActorId} deleted role {RoleId}", User.GetUserId(), id);
        return NoContent();
    }
}
