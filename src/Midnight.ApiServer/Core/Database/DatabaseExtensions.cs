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

        if (await SkipAlreadyAppliedExtensionPackagesAsync(context, pending, logger))
            pending = (await context.Database.GetPendingMigrationsAsync()).ToArray();

        if (pending.Length > 0)
        {
            logger.LogInformation("Applying {Count} database migration(s)", pending.Length);
            await context.Database.MigrateAsync();
        }
    }

    private static readonly string[] ExtensionPackageColumns = ["InstallSource", "SourceUrl", "PackageFileName", "StoragePath"];

    /// <summary>
    /// For a while the ExtensionPackages columns were part of the Extensions migration itself, so databases created
    /// in that time already have them and the ExtensionPackages migration would fail with "column already exists".
    /// Those databases get the migration marked as applied instead of running it.
    /// </summary>
    internal static async Task<bool> SkipAlreadyAppliedExtensionPackagesAsync(DataContext context, IReadOnlyCollection<string> pending, ILogger logger)
    {
        var migration = pending.FirstOrDefault(id => id.EndsWith("_ExtensionPackages", StringComparison.Ordinal));

        if (migration == null || await CountExistingColumnsAsync(context, "Extensions", ExtensionPackageColumns) != ExtensionPackageColumns.Length)
            return false;

        logger.LogWarning("The columns of migration {Migration} already exist, marking it as applied", migration);

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1})",
            migration,
            Microsoft.EntityFrameworkCore.Infrastructure.ProductInfo.GetVersion());

        return true;
    }

    private static async Task<int> CountExistingColumnsAsync(DataContext context, string table, string[] columns)
    {
        // Parameters can't be used inside pragma_table_info on every SQLite version, the names are constants anyway
        var names = string.Join(", ", columns.Select(c => $"'{c}'"));
        var sql = context.Database.IsNpgsql()
            ? $"SELECT COUNT(*)::int AS \"Value\" FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = '{table}' AND column_name IN ({names})"
            : $"SELECT COUNT(*) AS \"Value\" FROM pragma_table_info('{table}') WHERE name IN ({names})";

        return await context.Database.SqlQueryRaw<int>(sql).SingleAsync();
    }
}
