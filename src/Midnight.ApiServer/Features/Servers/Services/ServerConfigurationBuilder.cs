using System.Net.Sockets;
using Midnight.ApiServer.Features.Nodes.Entities;
using Midnight.ApiServer.Features.Servers.Entities;
using Midnight.ApiServer.Features.Shards.Services;
using Midnight.Shared.Http.Demon;
using Midnight.Shared.Http.Servers;
using Midnight.Shared.Http.Shards;

namespace Midnight.ApiServer.Features.Servers.Services;

/// <summary>
/// Turns a server into the configuration the demon runs it with. The server needs Node, Shard (with Variables),
/// Variables and Allocations loaded.
/// </summary>
public static class ServerConfigurationBuilder
{
    /// <summary>Stop command of imported Pterodactyl eggs that means "send SIGINT" instead of writing to stdin</summary>
    public const string CtrlCStopCommand = "^C";

    public static DemonServerConfiguration Build(Server server, string? timeZone)
    {
        var primary = GetPrimaryAllocation(server)
                      ?? throw new InvalidOperationException($"Server {server.Uuid} has no primary allocation");

        var shard = server.Shard;

        // Shard variables first, the system variables below win on name clashes
        var environment = new Dictionary<string, string>(GetVariableValues(server, viewableOnly: false), StringComparer.Ordinal)
        {
            ["SERVER_MEMORY"] = server.Memory.ToString(),
            ["SERVER_IP"] = primary.IpAddress,
            ["SERVER_PORT"] = primary.Port.ToString(),
            ["P_SERVER_UUID"] = server.Uuid.ToString(),
            ["P_SERVER_LOCATION"] = server.Node.Name,
            ["TZ"] = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone.Trim()
        };

        var startup = Resolve(GetStartupCommand(server), environment, server, primary);
        environment["STARTUP"] = startup;

        var configFiles = shard.GetConfigFiles().Select(file => new ShardConfigFileDto
        {
            Path = file.Path,
            Parser = file.Parser,
            Mappings = file.Mappings.ToDictionary(x => x.Key, x => Resolve(x.Value, environment, server, primary))
        }).ToList();

        var (stopCommand, stopSignal) = GetStop(shard.StopCommand, shard.StopSignal);

        return new DemonServerConfiguration
        {
            Uuid = server.Uuid,
            Name = server.Name,
            Suspended = server.Suspended,
            DockerImage = server.DockerImage,
            StartupCommand = startup,
            Environment = environment,
            Limits = ToLimits(server),
            PrimaryAllocation = new DemonAllocation { IpAddress = primary.IpAddress, Port = primary.Port },
            Allocations = GetAllocations(server)
                .Select(x => new DemonAllocation { IpAddress = x.IpAddress, Port = x.Port })
                .ToList(),
            Process = new DemonProcessConfiguration
            {
                StopCommand = stopCommand,
                StopSignal = stopSignal,
                StartupDone = SplitStartupDone(shard.StartupDone),
                ConfigFiles = configFiles,
                Features = shard.GetFeatures()
            },
            Install = new DemonInstallConfiguration
            {
                Script = string.IsNullOrWhiteSpace(shard.InstallScript) ? null : shard.InstallScript,
                Image = shard.InstallImage,
                Interpreter = shard.InstallInterpreter
            }
        };
    }

    /// <summary>
    /// The startup command as users see it: system variables and visible variables are filled in,
    /// placeholders of hidden variables stay as they are.
    /// </summary>
    public static string BuildStartupPreview(Server server)
    {
        var primary = GetPrimaryAllocation(server);
        var environment = GetVariableValues(server, viewableOnly: true);

        return StartupCommandResolver.ResolveStartupCommand(
            GetStartupCommand(server),
            environment,
            server.Memory,
            primary?.Port ?? 0,
            primary?.IpAddress ?? "0.0.0.0");
    }

    /// <summary>
    /// The shard's StartupDone field may contain several strings, one per line. Empty lines are ignored,
    /// commas are kept because they are common in log lines.
    /// </summary>
    public static List<string> SplitStartupDone(string? startupDone)
        => (startupDone ?? "")
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>"^C" (Pterodactyl's notation) becomes SIGINT, every other stop command is written to stdin</summary>
    public static (string? Command, string Signal) GetStop(string? stopCommand, string? stopSignal)
    {
        var signal = string.IsNullOrWhiteSpace(stopSignal) ? "SIGTERM" : stopSignal.Trim();

        if (string.IsNullOrWhiteSpace(stopCommand))
            return (null, signal);

        if (stopCommand.Trim() == CtrlCStopCommand)
            return (null, "SIGINT");

        return (stopCommand, signal);
    }

    /// <summary>EnvVariable => value of all (or only the user visible) shard variables, the default value when the server has none</summary>
    public static Dictionary<string, string> GetVariableValues(Server server, bool viewableOnly)
    {
        var values = server.Variables.ToDictionary(x => x.ShardVariableId, x => x.Value);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var variable in server.Shard.Variables.OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
        {
            if (viewableOnly && !variable.UserViewable)
                continue;

            result[variable.EnvVariable] = values.TryGetValue(variable.Id, out var value) ? value : variable.DefaultValue ?? "";
        }

        return result;
    }

    public static string GetStartupCommand(Server server)
        => string.IsNullOrWhiteSpace(server.StartupCommand) ? server.Shard.StartupCommand : server.StartupCommand;

    public static Allocation? GetPrimaryAllocation(Server server)
        => server.Allocations.FirstOrDefault(x => x.Id == server.PrimaryAllocationId) ?? server.PrimaryAllocation;

    /// <summary>All allocations of the server, the primary one first</summary>
    public static List<Allocation> GetAllocations(Server server)
    {
        var primary = GetPrimaryAllocation(server);
        var others = server.Allocations.Where(x => x.Id != primary?.Id).OrderBy(x => x.IpAddress).ThenBy(x => x.Port);

        return primary == null ? others.ToList() : [primary, ..others];
    }

    public static ServerLimitsDto ToLimits(Server server) => new()
    {
        Cpu = server.Cpu,
        Memory = server.Memory,
        Swap = server.Swap,
        Disk = server.Disk,
        IoWeight = server.IoWeight,
        Threads = server.Threads,
        OomKillerDisabled = server.OomKillerDisabled
    };

    /// <summary>"ip:port", ipv6 addresses in brackets</summary>
    public static string FormatAddress(string ipAddress, int port)
        => System.Net.IPAddress.TryParse(ipAddress, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6
            ? $"[{ipAddress}]:{port}"
            : $"{ipAddress}:{port}";

    private static string Resolve(string value, Dictionary<string, string> environment, Server server, Allocation primary)
        => StartupCommandResolver.ResolveStartupCommand(value, environment, server.Memory, primary.Port, primary.IpAddress);
}
