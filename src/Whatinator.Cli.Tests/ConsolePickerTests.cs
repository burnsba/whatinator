namespace Whatinator.Cli.Tests;

/// <summary>
/// Tests for <see cref="ConsolePicker"/> -- see
/// docs/backlog/018-consolepicker-non-tty-behaviour.md.
/// </summary>
[Collection("Console")]
public class ConsolePickerTests
{
    [Fact]
    public async Task PromptForSelectionAsync_OutputRedirected_ReturnsNullWithoutPromptingOrStdoutWrite()
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
            chosen = await ConsolePicker.PromptForSelectionAsync(
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
    public async Task PromptForSelectionAsync_OutputNotRedirected_StillPrompts()
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        Console.SetIn(new StringReader("2\n"));
        Console.SetOut(new StringWriter());

        string? chosen;
        try
        {
            chosen = await ConsolePicker.PromptForSelectionAsync(
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

    [Fact]
    public async Task PromptForSelectionAsync_CancelledToken_ThrowsInsteadOfHanging()
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;

        // A reader with nothing buffered: without cancellation flowing into
        // the read, this would block forever waiting for input that never
        // arrives -- see root CLAUDE.md § Gotchas on Ctrl-C handling.
        Console.SetIn(new StringReader(string.Empty));
        Console.SetOut(new StringWriter());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConsolePicker.PromptForSelectionAsync(
                "Found 2 matching releases:",
                new[] { "first", "second" },
                describe: static candidate => candidate,
                allowSkip: false,
                cancellationToken: cts.Token,
                isOutputRedirected: false));
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task PromptForSelectionAsync_ManualOverrideChosen_ReturnsOverriddenValueWithoutConsumingANumericChoice()
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        Console.SetIn(new StringReader("m\n"));
        Console.SetOut(new StringWriter());

        string? chosen;
        try
        {
            chosen = await ConsolePicker.PromptForSelectionAsync(
                "Found 2 matching releases:",
                new[] { "first", "second" },
                describe: static candidate => candidate,
                allowSkip: false,
                manualOverride: static _ => Task.FromResult<string?>("manual"),
                isOutputRedirected: false);
        }
        finally
        {
            Console.SetIn(originalIn);
            Console.SetOut(originalOut);
        }

        Assert.Equal("manual", chosen);
    }

    [Fact]
    public async Task PromptForSelectionAsync_ManualOverrideFails_ReshowsListAndAcceptsNumericChoice()
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;

        // First "m" fails (delegate returns null); then the re-shown prompt accepts "2".
        Console.SetIn(new StringReader("m\n2\n"));
        Console.SetOut(new StringWriter());

        string? chosen;
        try
        {
            chosen = await ConsolePicker.PromptForSelectionAsync(
                "Found 2 matching releases:",
                new[] { "first", "second" },
                describe: static candidate => candidate,
                allowSkip: false,
                manualOverride: static _ => Task.FromResult<string?>(null),
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
