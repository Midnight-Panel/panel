using Midnight.ApiServer.Features.Shards.Services;

namespace Midnight.Panel.Tests;

public class ShardVariableValidationTests
{
    [Fact]
    public void ValidateValue_Required_FailsWhenEmpty()
    {
        var (isValid, error) = ShardVariableValidator.ValidateValue("", "required|string", "SERVER_JARFILE");

        Assert.False(isValid);
        Assert.Contains("Pflichtfeld", error);
    }

    [Fact]
    public void ValidateValue_Required_PassesWhenProvided()
    {
        var (isValid, error) = ShardVariableValidator.ValidateValue("server.jar", "required|string", "SERVER_JARFILE");

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateValue_Optional_PassesWhenEmpty()
    {
        var (isValid, error) = ShardVariableValidator.ValidateValue("", "string|max:50", "OPTIONAL_VAR");

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("1024", true)]
    [InlineData("0", true)]
    [InlineData("-5", true)]
    [InlineData("abc", false)]
    [InlineData("12.34", false)]
    public void ValidateValue_Integer_ValidatesCorrectly(string value, bool expectedValid)
    {
        var (isValid, _) = ShardVariableValidator.ValidateValue(value, "required|integer", "PORT");
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("1024.5", true)]
    [InlineData("100", true)]
    [InlineData("abc", false)]
    public void ValidateValue_Numeric_ValidatesCorrectly(string value, bool expectedValid)
    {
        var (isValid, _) = ShardVariableValidator.ValidateValue(value, "required|numeric", "FLOAT_VAL");
        Assert.Equal(expectedValid, isValid);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", true)]
    [InlineData("1", true)]
    [InlineData("0", true)]
    [InlineData("yes", false)]
    [InlineData("invalid", false)]
    public void ValidateValue_Boolean_ValidatesCorrectly(string value, bool expectedValid)
    {
        var (isValid, _) = ShardVariableValidator.ValidateValue(value, "required|boolean", "ONLINE_MODE");
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void ValidateValue_MinMaxNumeric_ValidatesRange()
    {
        var rules = "required|numeric|min:1024|max:65535";

        var (validLow, _) = ShardVariableValidator.ValidateValue("500", rules, "PORT");
        var (validOk, _) = ShardVariableValidator.ValidateValue("25565", rules, "PORT");
        var (validHigh, _) = ShardVariableValidator.ValidateValue("70000", rules, "PORT");

        Assert.False(validLow);
        Assert.True(validOk);
        Assert.False(validHigh);
    }

    [Fact]
    public void ValidateValue_MinMaxString_ValidatesLength()
    {
        var rules = "required|string|min:3|max:10";

        var (validShort, _) = ShardVariableValidator.ValidateValue("ab", rules, "MOTD");
        var (validOk, _) = ShardVariableValidator.ValidateValue("Minecraft", rules, "MOTD");
        var (validLong, _) = ShardVariableValidator.ValidateValue("This is way too long", rules, "MOTD");

        Assert.False(validShort);
        Assert.True(validOk);
        Assert.False(validLong);
    }

    [Fact]
    public void ValidateValue_Between_ValidatesCorrectly()
    {
        var rules = "required|numeric|between:1,100";

        var (validBelow, _) = ShardVariableValidator.ValidateValue("0", rules, "PERCENT");
        var (validMid, _) = ShardVariableValidator.ValidateValue("50", rules, "PERCENT");
        var (validAbove, _) = ShardVariableValidator.ValidateValue("101", rules, "PERCENT");

        Assert.False(validBelow);
        Assert.True(validMid);
        Assert.False(validAbove);
    }

    [Fact]
    public void ValidateValue_Regex_ValidatesPattern()
    {
        var rules = @"required|regex:^[a-zA-Z0-9_-]+\.jar$";

        var (validPass, _) = ShardVariableValidator.ValidateValue("paper-1.20.4.jar", rules, "JAR");
        var (validFail, _) = ShardVariableValidator.ValidateValue("paper/../../etc/passwd", rules, "JAR");

        Assert.True(validPass);
        Assert.False(validFail);
    }
}
