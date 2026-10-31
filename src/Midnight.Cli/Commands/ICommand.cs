namespace Midnight.Cli.Commands;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
    Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default);
}
