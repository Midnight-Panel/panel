using Midnight.ApiServer.Features.Shards.Services;

namespace Midnight.Panel.Tests;

public class StartupCommandResolverTests
{
    [Fact]
    public void ExtractPlaceholders_FindsAllVariables()
    {
        var command = "java -Xms128M -Xmx{{SERVER_MEMORY}}M -Dterminal.jline=false -jar {{SERVER_JARFILE}} --port {{SERVER_PORT}}";
        var placeholders = StartupCommandResolver.ExtractPlaceholders(command);

        Assert.Equal(3, placeholders.Count);
        Assert.Contains("SERVER_MEMORY", placeholders);
        Assert.Contains("SERVER_JARFILE", placeholders);
        Assert.Contains("SERVER_PORT", placeholders);
    }

    [Fact]
    public void ValidatePlaceholders_AcceptsSystemVariablesAndDefinedVariables()
    {
        var command = "java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}} --port {{SERVER_PORT}}";
        var defined = new[] { "SERVER_JARFILE" };

        var errors = StartupCommandResolver.ValidatePlaceholders(command, defined);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidatePlaceholders_DetectsUndefinedVariables()
    {
        var command = "java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}} --world {{WORLD_NAME}}";
        var defined = new[] { "SERVER_JARFILE" };

        var errors = StartupCommandResolver.ValidatePlaceholders(command, defined);

        Assert.Single(errors);
        Assert.Contains("WORLD_NAME", errors[0]);
    }

    [Fact]
    public void ResolveStartupCommand_ReplacesPlaceholdersWithValues()
    {
        var command = "java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}} --port {{SERVER_PORT}} --ip {{SERVER_IP}}";
        var env = new Dictionary<string, string>
        {
            ["SERVER_JARFILE"] = "paper.jar"
        };

        var resolved = StartupCommandResolver.ResolveStartupCommand(command, env, memoryMb: 4096, port: 25565, ip: "192.168.1.100");

        Assert.Equal("java -Xmx4096M -jar paper.jar --port 25565 --ip 192.168.1.100", resolved);
    }

    [Fact]
    public void ResolveStartupCommand_KeepsUnknownPlaceholdersIntact()
    {
        var command = "./start --arg {{CUSTOM_UNKNOWN}}";
        var env = new Dictionary<string, string>();

        var resolved = StartupCommandResolver.ResolveStartupCommand(command, env);

        Assert.Equal("./start --arg {{CUSTOM_UNKNOWN}}", resolved);
    }
}
