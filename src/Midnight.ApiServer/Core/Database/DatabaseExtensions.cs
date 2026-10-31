using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Midnight.ApiServer.Core.Configuration;

namespace Midnight.ApiServer.Core.Database;

public static class DatabaseExtensions
{
    public static IServiceCollection AddMidnightDatabase(this IServiceCollection services, AppConfiguration configuration, string storagePath)
    {
        var database = configuration.Database;

        if (database.Provider.Equals(DatabaseProviders.PostgreSql, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(database.ConnectionString))
                throw new InvalidOperationException("Database.ConnectionString is required for PostgreSQL");

            services.AddDbContext<PostgresDataContext>(options =>
            {
                options.UseNpgsql(database.ConnectionString);
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            });
            services.AddScoped<DataContext>(provider => provider.GetRequiredService<PostgresDataContext>());
        }
        else if (database.Provider.Equals(DatabaseProviders.Sqlite, StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = string.IsNullOrWhiteSpace(database.ConnectionString)
                ? $"Data Source={Path.Combine(storagePath, "midnight.db")}"
                : database.ConnectionString;

            services.AddDbContext<SqliteDataContext>(options =>
            {
                options.UseSqlite(connectionString);
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            });
            services.AddScoped<DataContext>(provider => provider.GetRequiredService<SqliteDataContext>());
        }
        else
        {
            throw new InvalidOperationException($"Unknown database provider '{database.Provider}'. Use '{DatabaseProviders.Sqlite}' or '{DatabaseProviders.PostgreSql}'");
        }

        return services;
    }

    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataContext>>();

        var pending = (await context.Database.GetPendingMigrationsAsync()).ToArray();

        if (pending.Length > 0)
        {
            logger.LogInformation("Applying {Count} database migration(s)", pending.Length);
            await context.Database.MigrateAsync();
        }
    }
}
