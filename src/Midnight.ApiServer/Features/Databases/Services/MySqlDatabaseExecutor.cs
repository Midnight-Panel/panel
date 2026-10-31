using MySqlConnector;

namespace Midnight.ApiServer.Features.Databases.Services;

/// <summary>
/// MySqlConnector based executor. Identifiers (database, user, host) can't be parameters in MySQL: they are validated by the
/// services ([a-zA-Z0-9_] names, remote pattern of CreateServerDatabaseRequest) and additionally escaped with backticks.
/// Passwords are passed as parameters.
/// </summary>
public class MySqlDatabaseExecutor : IDatabaseExecutor
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    private readonly ILogger<MySqlDatabaseExecutor> _logger;

    public MySqlDatabaseExecutor(ILogger<MySqlDatabaseExecutor> logger)
    {
        _logger = logger;
    }

    public async Task CreateDatabaseAsync(DatabaseHostConnection host, string database, string username, string remote, string password, CancellationToken cancellationToken)
    {
        await RunAsync(host, async connection =>
        {
            var databaseCreated = false;
            var userCreated = false;

            try
            {
                await ExecuteAsync(connection, $"CREATE DATABASE {QuoteIdentifier(database)}", null, cancellationToken);
                databaseCreated = true;

                await ExecuteAsync(connection, $"CREATE USER {Account(username, remote)} IDENTIFIED BY @password", password, cancellationToken);
                userCreated = true;

                await ExecuteAsync(connection, $"GRANT ALL PRIVILEGES ON {QuoteGrantDatabase(database)}.* TO {Account(username, remote)}", null, cancellationToken);
            }
            catch
            {
                // Only undo what this call created, an existing database with the same name is never touched
                if (userCreated)
                    await TryExecuteAsync(connection, $"DROP USER IF EXISTS {Account(username, remote)}");

                if (databaseCreated)
                    await TryExecuteAsync(connection, $"DROP DATABASE IF EXISTS {QuoteIdentifier(database)}");

                throw;
            }

            return 0;
        }, null, cancellationToken);
    }

    public Task ChangePasswordAsync(DatabaseHostConnection host, string username, string remote, string password, CancellationToken cancellationToken)
        => RunAsync(host, async connection =>
        {
            await ExecuteAsync(connection, $"ALTER USER {Account(username, remote)} IDENTIFIED BY @password", password, cancellationToken);
            return 0;
        }, null, cancellationToken);

    public Task DropDatabaseAsync(DatabaseHostConnection host, string database, string username, string remote, CancellationToken cancellationToken)
        => RunAsync(host, async connection =>
        {
            await ExecuteAsync(connection, $"DROP USER IF EXISTS {Account(username, remote)}", null, cancellationToken);
            await ExecuteAsync(connection, $"DROP DATABASE IF EXISTS {QuoteIdentifier(database)}", null, cancellationToken);
            return 0;
        }, null, cancellationToken);

    public Task<string> TestAsync(DatabaseHostConnection host, CancellationToken cancellationToken)
        => RunAsync(host, async connection =>
        {
            await using var command = new MySqlCommand("SELECT VERSION()", connection);
            return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? "";
        }, TestTimeout, cancellationToken);

    // ─── Quoting ────────────────────────────────────────────────────────────

    /// <summary>`name` with backticks doubled</summary>
    public static string QuoteIdentifier(string value) => "`" + value.Replace("`", "``") + "`";

    /// <summary>`user`@`host`</summary>
    public static string Account(string username, string remote) => QuoteIdentifier(username) + "@" + QuoteIdentifier(remote);

    /// <summary>_ and % are wildcards in the database name of GRANT statements, escaped so the grant matches exactly this database</summary>
    public static string QuoteGrantDatabase(string database)
        => QuoteIdentifier(database.Replace("\\", "\\\\").Replace("_", "\\_").Replace("%", "\\%"));

    // ─── Helpers ────────────────────────────────────────────────────────────

    private async Task<T> RunAsync<T>(DatabaseHostConnection host, Func<MySqlConnection, Task<T>> action, TimeSpan? timeout, CancellationToken cancellationToken)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = host.Host,
            Port = (uint)host.Port,
            UserID = host.Username,
            Password = host.Password,
            ConnectionTimeout = (uint)(timeout ?? TimeSpan.FromSeconds(10)).TotalSeconds,
            DefaultCommandTimeout = 30,
            SslMode = MySqlSslMode.Preferred,
            // MySQL 8 caching_sha2_password without TLS needs the server's public key
            AllowPublicKeyRetrieval = true,
            Pooling = false
        };

        try
        {
            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return await action(connection);
        }
        catch (Exception e) when (e is MySqlException or InvalidOperationException or System.Net.Sockets.SocketException or TimeoutException ||
                                  (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning("Database host {Host}:{Port} failed: {Error}", host.Host, host.Port, e.Message);
            throw new DatabaseHostException(e.Message, e);
        }
    }

    private static async Task ExecuteAsync(MySqlConnection connection, string sql, string? password, CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(sql, connection);

        if (password != null)
            command.Parameters.AddWithValue("@password", password);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task TryExecuteAsync(MySqlConnection connection, string sql)
    {
        try
        {
            await ExecuteAsync(connection, sql, null, CancellationToken.None);
        }
        catch (MySqlException e)
        {
            _logger.LogWarning("Cleanup statement failed: {Error}", e.Message);
        }
    }
}
