using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Midnight.ApiServer.Core.Database;

namespace Midnight.Panel.Tests;

/// <summary>
/// The ExtensionPackages columns were briefly part of the Extensions migration. Both kinds of databases
/// (with and without the columns) must reach the latest migration.
/// </summary>
public sealed class MigrationTests : IAsyncLifetime
{
    private const string ExtensionPackages = "20261031224034_ExtensionPackages";
    private const string BeforeExtensionPackages = "20261031221847_ServerFeatures";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private SqliteDataContext _context = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _context = new SqliteDataContext(new DbContextOptionsBuilder<SqliteDataContext>().UseSqlite(_connection).Options);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task DatabaseWithTheColumnsSkipsTheMigration()
    {
        // Like a database created from the edited Extensions migration: columns there, migration not recorded
        await _context.Database.MigrateAsync();
        await _context.Database.ExecuteSqlRawAsync($"DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{ExtensionPackages}'");
        var pending = (await _context.Database.GetPendingMigrationsAsync()).ToArray();

        var skipped = await DatabaseExtensions.SkipAlreadyAppliedExtensionPackagesAsync(_context, pending, NullLogger.Instance);

        Assert.True(skipped);
        Assert.Empty(await _context.Database.GetPendingMigrationsAsync());
        await _context.Database.MigrateAsync();
    }

    [Fact]
    public async Task DatabaseWithoutTheColumnsRunsTheMigration()
    {
        await _context.GetService<IMigrator>().MigrateAsync(BeforeExtensionPackages);
        var pending = (await _context.Database.GetPendingMigrationsAsync()).ToArray();

        var skipped = await DatabaseExtensions.SkipAlreadyAppliedExtensionPackagesAsync(_context, pending, NullLogger.Instance);

        Assert.False(skipped);
        Assert.Contains(ExtensionPackages, pending);
        await _context.Database.MigrateAsync();
        Assert.Empty(await _context.Database.GetPendingMigrationsAsync());
        Assert.Equal(1, await _context.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM pragma_table_info('Extensions') WHERE name = 'InstallSource'").SingleAsync());
    }

    [Fact]
    public async Task FreshDatabaseIsNotTouched()
    {
        var pending = (await _context.Database.GetPendingMigrationsAsync()).ToArray();

        Assert.False(await DatabaseExtensions.SkipAlreadyAppliedExtensionPackagesAsync(_context, pending, NullLogger.Instance));
        await _context.Database.MigrateAsync();
    }
}
