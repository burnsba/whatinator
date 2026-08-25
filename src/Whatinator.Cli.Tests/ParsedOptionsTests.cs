namespace Whatinator.Cli.Tests;

/// <summary>Tests for <see cref="ParsedOptions"/> -- see docs/backlog-completed/006-unknown-options-silently-ignored.md.</summary>
public class ParsedOptionsTests
{
    [Fact]
    public void Parse_KnownValueOption_ReturnsValue()
    {
        var result = ParsedOptions.Parse(["--dest", "out/"], OptionSpec.Value("--dest"));

        Assert.False(result.HasErrors);
        Assert.Equal("out/", result.GetValue("--dest"));
    }

    [Fact]
    public void Parse_KnownFlag_IsSet()
    {
        var result = ParsedOptions.Parse(["--ask"], OptionSpec.Flag("--ask"));

        Assert.False(result.HasErrors);
        Assert.True(result.HasFlag("--ask"));
    }

    [Fact]
    public void Parse_ShortName_ResolvesToLongName()
    {
        var result = ParsedOptions.Parse(["-e", "value"], OptionSpec.Value("--example", "-e"));

        Assert.False(result.HasErrors);
        Assert.Equal("value", result.GetValue("--example"));
    }

    [Fact]
    public void Parse_UnknownOption_IsAnError()
    {
        var result = ParsedOptions.Parse(["--dsc", "2"], OptionSpec.Value("--disc"));

        Assert.True(result.HasErrors);
        Assert.Contains("Unknown option: --dsc", result.Errors);
    }

    [Fact]
    public void Parse_UnexpectedPositionalArgument_IsAnError()
    {
        var result = ParsedOptions.Parse(["extra-arg"]);

        Assert.True(result.HasErrors);
        Assert.Contains("Unexpected argument: extra-arg", result.Errors);
    }

    [Fact]
    public void Parse_ValueOptionMissingItsValue_IsAnError()
    {
        var result = ParsedOptions.Parse(["--dest"], OptionSpec.Value("--dest"));

        Assert.True(result.HasErrors);
        Assert.Contains("--dest requires a value.", result.Errors);
    }

    [Fact]
    public void Parse_ValueOptionFollowedByAnotherOption_TreatsItAsMissingValue()
    {
        var result = ParsedOptions.Parse(
            ["--releaseinfo", "--keep-wav"],
            OptionSpec.Value("--releaseinfo"),
            OptionSpec.Flag("--keep-wav"));

        Assert.True(result.HasErrors);
        Assert.Contains("--releaseinfo requires a value.", result.Errors);
    }

    [Fact]
    public void Parse_DuplicatedValueOption_IsAnError()
    {
        var result = ParsedOptions.Parse(["--disc", "1", "--disc", "2"], OptionSpec.Value("--disc"));

        Assert.True(result.HasErrors);
        Assert.Contains("--disc given more than once.", result.Errors);
    }

    [Fact]
    public void Parse_DuplicatedFlag_IsAnError()
    {
        var result = ParsedOptions.Parse(["--ask", "--ask"], OptionSpec.Flag("--ask"));

        Assert.True(result.HasErrors);
        Assert.Contains("--ask given more than once.", result.Errors);
    }

    [Fact]
    public void Parse_Debug_IsAlwaysKnownEvenWhenNotDeclared()
    {
        var result = ParsedOptions.Parse(["--debug"], OptionSpec.Value("--dest"));

        Assert.False(result.HasErrors);
        Assert.True(result.HasFlag("--debug"));
    }

    [Fact]
    public void Parse_NoArguments_HasNoErrors()
    {
        var result = ParsedOptions.Parse([]);

        Assert.False(result.HasErrors);
    }
}
