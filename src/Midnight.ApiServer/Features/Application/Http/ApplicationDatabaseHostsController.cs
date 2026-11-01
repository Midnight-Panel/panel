using Microsoft.AspNetCore.Mvc;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Databases.Services;
using Midnight.Shared.Http;
using Midnight.Shared.Http.Admin;
using Midnight.Shared.Http.Application;

namespace Midnight.ApiServer.Features.Application.Http;

/// <summary>Database hosts, read only. Passwords are never returned.</summary>
[Route(RoutePrefix + "/database-hosts")]
public class ApplicationDatabaseHostsController : ApplicationControllerBase
{
    private readonly DatabaseHostService _hosts;

    public ApplicationDatabaseHostsController(DatabaseHostService hosts)
    {
        _hosts = hosts;
    }

    [HttpGet]
    [ApplicationAccess(ApplicationResource.DatabaseHosts, ApplicationAccess.Read)]
    public async Task<PagedResult<DatabaseHostResponse>> List([FromQuery] PagedQuery query, CancellationToken cancellationToken)
    {
        // Only a handful of hosts exist, paged in memory
        var hosts = await _hosts.ListAsync(cancellationToken);

        return new PagedResult<DatabaseHostResponse>
        {
            Items = hosts.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList(),
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            TotalCount = hosts.Count
        };
    }

    [HttpGet("{id:int}")]
    [ApplicationAccess(ApplicationResource.DatabaseHosts, ApplicationAccess.Read)]
    public Task<DatabaseHostResponse> Get(int id, CancellationToken cancellationToken)
        => _hosts.GetResponseAsync(id, cancellationToken);
}
