using Midnight.Shared;

namespace Midnight.Panel.Tests;

public class PermissionsTests
{
    [Theory]
    [InlineData("*", "admin.users.read", true)]
    [InlineData("admin.*", "admin.users.read", true)]
    [InlineData("admin.*", "admin", true)]
    [InlineData("admin.users.*", "admin.users.write", true)]
    [InlineData("admin.users.read", "admin.users.read", true)]
    [InlineData("admin.users.read", "admin.users.write", false)]
    [InlineData("admin.users.*", "admin.roles.read", false)]
    [InlineData("admin.*", "administrator.read", false)]
    [InlineData("admin", "admin.users.read", false)]
    [InlineData("", "admin.users.read", false)]
    public void Matches_HandlesWildcards(string granted, string required, bool expected)
    {
        Assert.Equal(expected, Permissions.Matches(granted, required));
    }

    [Fact]
    public void Has_ReturnsTrueIfAnyPermissionMatches()
    {
        string[] granted = ["account.read", "admin.users.*"];

        Assert.True(Permissions.Has(granted, "admin.users.impersonate"));
        Assert.False(Permissions.Has(granted, "admin.nodes.read"));
        Assert.False(Permissions.Has([], "account.read"));
    }

    [Theory]
    [InlineData("*", true)]
    [InlineData("admin.users.read", true)]
    [InlineData("admin.*", true)]
    [InlineData("server-files.read_only", true)]
    [InlineData("Admin.users", false)]
    [InlineData("admin..users", false)]
    [InlineData("*.users", false)]
    [InlineData("admin.*.read", false)]
    [InlineData("admin users", false)]
    [InlineData("", false)]
    public void IsValid_ChecksFormat(string permission, bool expected)
    {
        Assert.Equal(expected, Permissions.IsValid(permission));
    }

    [Fact]
    public void AllKnownPermissionsAreValid()
    {
        Assert.All(Permissions.All, permission => Assert.True(Permissions.IsValid(permission), permission));
    }
}
