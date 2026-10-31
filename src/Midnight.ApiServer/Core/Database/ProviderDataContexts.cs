using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Midnight.ApiServer.Core.Database;

public class SqliteDataContext : DataContext
{
    public SqliteDataContext(DbContextOptions<SqliteDataContext> options) : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Sqlite can't order or compare DateTimeOffset values, store them as unix milliseconds (UTC)
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToUnixMillisecondsConverter>();
    }
}

public class PostgresDataContext : DataContext
{
    public PostgresDataContext(DbContextOptions<PostgresDataContext> options) : base(options)
    {
    }
}

public class DateTimeOffsetToUnixMillisecondsConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToUnixMillisecondsConverter() : base(
        value => value.ToUnixTimeMilliseconds(),
        value => DateTimeOffset.FromUnixTimeMilliseconds(value))
    {
    }
}

// Used by "dotnet ef migrations add" only

public class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteDataContext>
{
    public SqliteDataContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SqliteDataContext>()
            .UseSqlite("Data Source=design.db")
            .Options;

        return new SqliteDataContext(options);
    }
}

public class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresDataContext>
{
    public PostgresDataContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PostgresDataContext>()
            .UseNpgsql("Host=localhost;Database=midnight_design")
            .Options;

        return new PostgresDataContext(options);
    }
}
