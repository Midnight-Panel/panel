namespace Midnight.ApiServer.Features.Databases.Services;

/// <summary>Connection data of a database host with the decrypted password</summary>
public record DatabaseHostConnection(string Host, int Port, string Username, string Password);

/// <summary>Runs the statements on a MySQL / MariaDB host. Behind an interface so the services can be tested without a server.</summary>
public interface IDatabaseExecutor
{
    /// <summary>Creates the database, the user username@remote and grants all privileges on the database. Cleans up what it created on failure.</summary>
    Task CreateDatabaseAsync(DatabaseHostConnection host, string database, string username, string remote, string password, CancellationToken cancellationToken);

    Task ChangePasswordAsync(DatabaseHostConnection host, string username, string remote, string password, CancellationToken cancellationToken);

    /// <summary>Drops the user and the database, missing ones are ignored</summary>
    Task DropDatabaseAsync(DatabaseHostConnection host, string database, string username, string remote, CancellationToken cancellationToken);

    /// <summary>Connects with a short timeout and returns the server version</summary>
    Task<string> TestAsync(DatabaseHostConnection host, CancellationToken cancellationToken);
}

/// <summary>The database host could not be reached or rejected a statement</summary>
public class DatabaseHostException(string message, Exception? innerException = null) : Exception(message, innerException);
