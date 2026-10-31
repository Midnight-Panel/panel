using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Midnight.ApiServer.Core.Database;
using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Shards.Entities;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Shards.Services;

public partial class ShardVariableService : IShardVariableService
{
    private readonly DataContext _context;
    private readonly TimeProvider _timeProvider;

    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex EnvVariableRegex();

    public ShardVariableService(DataContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<List<ShardVariableResponse>> ListByShardAsync(int shardId, CancellationToken cancellationToken)
    {
        await EnsureShardExistsAsync(shardId, cancellationToken);

        return await _context.ShardVariables
            .AsNoTracking()
            .Where(x => x.ShardId == shardId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ShardVariable> GetAsync(int shardId, int id, CancellationToken cancellationToken)
        => await _context.ShardVariables.FirstOrDefaultAsync(x => x.ShardId == shardId && x.Id == id, cancellationToken)
           ?? throw HttpApiException.NotFound("Shard-Variable nicht gefunden");

    public async Task<ShardVariableResponse> CreateAsync(int shardId, ShardVariableRequest request, CancellationToken cancellationToken)
    {
        var shard = await _context.Shards.FirstOrDefaultAsync(x => x.Id == shardId, cancellationToken)
                    ?? throw HttpApiException.NotFound("Shard nicht gefunden");

        var envVar = ValidateAndNormalizeEnv(request.EnvVariable);
        await EnsureUniqueEnvAsync(shardId, envVar, null, cancellationToken);

        var variable = new ShardVariable
        {
            ShardId = shardId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            EnvVariable = envVar,
            DefaultValue = request.DefaultValue?.Trim(),
            UserViewable = request.UserViewable,
            UserEditable = request.UserEditable,
            Rules = string.IsNullOrWhiteSpace(request.Rules) ? "required|string" : request.Rules.Trim(),
            SortOrder = request.SortOrder
        };

        shard.UpdatedAt = _timeProvider.GetUtcNow();
        _context.ShardVariables.Add(variable);
        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(variable);
    }

    public async Task<ShardVariableResponse> UpdateAsync(int shardId, int id, ShardVariableRequest request, CancellationToken cancellationToken)
    {
        var shard = await _context.Shards.FirstOrDefaultAsync(x => x.Id == shardId, cancellationToken)
                    ?? throw HttpApiException.NotFound("Shard nicht gefunden");

        var variable = await GetAsync(shardId, id, cancellationToken);

        var envVar = ValidateAndNormalizeEnv(request.EnvVariable);
        await EnsureUniqueEnvAsync(shardId, envVar, id, cancellationToken);

        variable.Name = request.Name.Trim();
        variable.Description = request.Description?.Trim();
        variable.EnvVariable = envVar;
        variable.DefaultValue = request.DefaultValue?.Trim();
        variable.UserViewable = request.UserViewable;
        variable.UserEditable = request.UserEditable;
        variable.Rules = string.IsNullOrWhiteSpace(request.Rules) ? "required|string" : request.Rules.Trim();
        variable.SortOrder = request.SortOrder;

        shard.UpdatedAt = _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(variable);
    }

    public async Task DeleteAsync(int shardId, int id, CancellationToken cancellationToken)
    {
        var shard = await _context.Shards.FirstOrDefaultAsync(x => x.Id == shardId, cancellationToken)
                    ?? throw HttpApiException.NotFound("Shard nicht gefunden");

        var variable = await GetAsync(shardId, id, cancellationToken);

        // Prüfen, ob die Variable noch im Startup-Befehl referenziert wird
        var placeholders = StartupCommandResolver.ExtractPlaceholders(shard.StartupCommand);
        if (placeholders.Contains(variable.EnvVariable))
        {
            throw HttpApiException.Conflict(
                "Variable wird noch verwendet",
                $"Die Variable '{variable.EnvVariable}' wird aktuell im Startbefehl des Shards referenziert.");
        }

        shard.UpdatedAt = _timeProvider.GetUtcNow();
        _context.ShardVariables.Remove(variable);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string ValidateAndNormalizeEnv(string env)
    {
        var trimmed = env?.Trim().ToUpperInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(trimmed))
            throw HttpApiException.BadRequest("Die Umgebungsvariable darf nicht leer sein.");

        if (!EnvVariableRegex().IsMatch(trimmed))
            throw HttpApiException.BadRequest("Der Variablenname darf nur Buchstaben, Zahlen und Unterstriche enthalten und nicht mit einer Zahl beginnen.");

        if (StartupCommandResolver.SystemVariables.Contains(trimmed))
            throw HttpApiException.BadRequest($"'{trimmed}' ist ein reservierter System-Variablenname.");

        return trimmed;
    }

    private async Task EnsureUniqueEnvAsync(int shardId, string env, int? exceptId, CancellationToken cancellationToken)
    {
        if (await _context.ShardVariables.AnyAsync(x => x.ShardId == shardId && x.Id != exceptId && x.EnvVariable == env, cancellationToken))
            throw HttpApiException.Conflict($"Eine Variable mit dem Namen '{env}' existiert für diesen Shard bereits.");
    }

    private async Task EnsureShardExistsAsync(int shardId, CancellationToken cancellationToken)
    {
        if (!await _context.Shards.AnyAsync(x => x.Id == shardId, cancellationToken))
            throw HttpApiException.NotFound("Shard nicht gefunden");
    }

    private static ShardVariableResponse ToResponse(ShardVariable v) => new()
    {
        Id = v.Id,
        ShardId = v.ShardId,
        Name = v.Name,
        Description = v.Description,
        EnvVariable = v.EnvVariable,
        DefaultValue = v.DefaultValue,
        UserViewable = v.UserViewable,
        UserEditable = v.UserEditable,
        Rules = v.Rules,
        SortOrder = v.SortOrder
    };
}
