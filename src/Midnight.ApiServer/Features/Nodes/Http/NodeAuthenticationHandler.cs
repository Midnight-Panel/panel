using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.Shared.Protocol;

namespace Midnight.ApiServer.Features.Nodes.Http;

/// <summary>Authenticates demons calling the panel with "Authorization: Bearer mdn_{tokenId}.{token}"</summary>
public class NodeAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Node";
    public const string NodeIdClaim = "node_id";

    private readonly NodeService _nodeService;

    public NodeAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, NodeService nodeService)
        : base(options, logger, encoder)
    {
        _nodeService = nodeService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!NodeToken.TryParse(ApiKeyAuthenticationHandler.GetBearerToken(Request), out var tokenId, out var token))
            return AuthenticateResult.NoResult();

        if (Request.Headers.TryGetValue(MidnightProtocol.HeaderName, out var protocol) && protocol.ToString() != MidnightProtocol.Version.ToString())
            return AuthenticateResult.Fail($"Unsupported protocol version {protocol}");

        var node = await _nodeService.AuthenticateAsync(tokenId, token, Context.RequestAborted);

        if (node == null)
        {
            Logger.LogWarning("Rejected demon request with invalid token from {Ip}", Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("Invalid node token");
        }

        var identity = new ClaimsIdentity([new Claim(NodeIdClaim, node.Id.ToString())], SchemeName, NodeIdClaim, null);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
