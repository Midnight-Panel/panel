using Midnight.ApiServer.Core.Http;
using Midnight.ApiServer.Features.Nodes.Services;
using Midnight.Shared.Protocol;

namespace Midnight.Panel.Tests;

public class AllocationPortParserTests
{
    [Fact]
    public void ParsePorts_HandlesListsAndRanges()
    {
        Assert.Equal([25565, 25570, 25571, 25572], AllocationService.ParsePorts("25565, 25570-25572"));
    }

    [Fact]
    public void ParsePorts_RemovesDuplicatesAndSorts()
    {
        Assert.Equal([80, 443, 8080], AllocationService.ParsePorts("8080;443 80,443"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("100-50")]
    [InlineData("1-2-3")]
    [InlineData("1-2000")]
    public void ParsePorts_RejectsInvalidInput(string input)
    {
        Assert.Throws<HttpApiException>(() => AllocationService.ParsePorts(input));
    }
}

public class NodeTokenTests
{
    [Fact]
    public void FormatAndParse_RoundTrip()
    {
        var value = NodeToken.Format("abc123", "secret");

        Assert.Equal("mdn_abc123.secret", value);
        Assert.True(NodeToken.TryParse(value, out var tokenId, out var token));
        Assert.Equal("abc123", tokenId);
        Assert.Equal("secret", token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc.def")]
    [InlineData("mdn_.secret")]
    [InlineData("mdn_abc.")]
    [InlineData("mdn_abc")]
    [InlineData("mdp_abc_def")]
    public void TryParse_RejectsInvalidValues(string? value)
    {
        Assert.False(NodeToken.TryParse(value, out _, out _));
    }
}
