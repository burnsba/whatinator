namespace Whatinator.Cli.Tests;

/// <summary>
/// Tests for <see cref="ConsolePicker"/> -- see
/// docs/backlog/018-consolepicker-non-tty-behaviour.md.
/// </summary>
[Collection("Console")]
public class ConsolePickerTests
{
    [Fact]
    public void PromptForSelection_OutputRedirected_ReturnsNullWithoutPromptingOrStdoutWrite()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);

        string? chosen;
        try
        {
            chosen = ConsolePicker.PromptForSelection(
                "Found 2 matching releases:",
                new[] { "first", "second" },
                describe: static candidate => candidate,
                allowSkip: false,
                isOutputRedirected: true);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.Null(chosen);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("2 candidates matched, but stdout is redirected", stderr.ToString());
    }

    [Fact]
    public void PromptForSelection_OutputNotRedirected_StillPrompts()
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        Console.SetIn(new StringReader("2\n"));
        Console.SetOut(new StringWriter());

        string? chosen;
        try
        {
            chosen = ConsolePicker.PromptForSelection(
                "Found 2 matching releases:",
                new[] { "first", "second" },
                describe: static candidate => candidate,
                allowSkip: false,
                isOutputRedirected: false);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }

        Assert.Equal("second", chosen);
    }
}
