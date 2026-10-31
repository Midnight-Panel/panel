using Microsoft.Extensions.Options;
using Midnight.ApiServer.Core.Configuration;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Core.Security;
using Midnight.ApiServer.Features.Users.Entities;
using Midnight.Shared;
using Midnight.Shared.Http.Account;

namespace Midnight.ApiServer.Features.Account.Services;

public class ApiKeyService
{
    public const int MaxKeysPerUser = 25;

    private readonly DataContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<AppConfiguration> _configuration;

    public ApiKeyService(DataContext context, TimeProvider timeProvider, IOptionsMonitor<AppConfiguration> configuration)
    {
        _context = context;
        _timeProvider = timeProvider;
        _configuration = configuration;
    }

    public async Task<List<ApiKeyResponse>> ListAsync(int userId, CancellationToken cancellationToken)
    {
        var keys = await _context.ApiKeys
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return keys.Select(ToResponse).ToList();
    }

    /// <param name="ownerPermissions">Permissions of the creating user, scopes can't exceed them</param>
    public async Task<CreateApiKeyResponse> CreateAsync(int userId, IEnumerable<string> ownerPermissions, CreateApiKeyRequest request, CancellationToken cancellationToken)
    {
        var granted = ownerPermissions.ToList();

        if (!_configuration.CurrentValue.Api.UserApiEnabled && !Permissions.Has(granted, Permissions.AdminSystemWrite))
            throw HttpApiException.Forbidden("User API access is currently disabled by administrator.");

        var now = _timeProvider.GetUtcNow();

        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= now)
            throw HttpApiException.BadRequest("The expiry date has to be in the future");

        var scopes = request.Scopes.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToList();

        if (scopes.Count == 0)
            throw HttpApiException.BadRequest("Select at least one scope");

        var granted = ownerPermissions.ToList();

        foreach (var scope in scopes)
        {
            if (!Permissions.IsValid(scope))
                throw HttpApiException.BadRequest("Invalid scope", $"'{scope}' is not a valid scope");

            // Wildcard scopes are fine, the owner's permissions are checked again on every request.
            // Concrete scopes the owner doesn't have are rejected to avoid confusion.
            if (!scope.EndsWith('*') && !Permissions.Has(granted, scope))
                throw HttpApiException.BadRequest("Invalid scope", $"You don't have the permission '{scope}'");
        }

        if (await _context.ApiKeys.CountAsync(x => x.UserId == userId, cancellationToken) >= MaxKeysPerUser)
            throw HttpApiException.BadRequest($"You can't have more than {MaxKeysPerUser} api keys");

        var prefix = SecureTokens.GenerateAlphanumeric(8);
        var key = $"{ApiKeyAuthenticationHandler.KeyPrefix}{prefix}_{SecureTokens.GenerateAlphanumeric(40)}";

        var apiKey = new ApiKey
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Prefix = prefix,
            KeyHash = SecureTokens.Hash(key),
            Scopes = scopes,
            CreatedAt = now,
            ExpiresAt = request.ExpiresAt
        };

        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateApiKeyResponse
        {
            ApiKey = ToResponse(apiKey),
            Key = key
        };
    }

    public async Task DeleteAsync(int userId, int id, CancellationToken cancellationToken)
    {
        var apiKey = await _context.ApiKeys.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken)
                     ?? throw HttpApiException.NotFound("Api key not found");

        _context.ApiKeys.Remove(apiKey);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static ApiKeyResponse ToResponse(ApiKey apiKey) => new()
    {
        Id = apiKey.Id,
        Name = apiKey.Name,
        Prefix = apiKey.Prefix,
        Scopes = apiKey.Scopes,
        CreatedAt = apiKey.CreatedAt,
        ExpiresAt = apiKey.ExpiresAt,
        LastUsedAt = apiKey.LastUsedAt
    };
}
