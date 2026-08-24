using System.Net.Http;

namespace Whatinator.Cli.Tests;

/// <summary>
/// Marks tests that redirect the process-global <see cref="Console"/> streams
/// so xunit runs them sequentially rather than in parallel with each other --
/// see <see cref="ConsoleTestCollection"/>.
/// </summary>
[CollectionDefinition("Console")]
public class ConsoleTestCollection
{
}

/// <summary>
/// Tests for <see cref="CommandDispatcher"/>'s error paths -- see
/// docs/backlog-completed/006-unknown-options-silently-ignored.md and
/// docs/backlog-completed/030-unknown-command-help-goes-to-stdout.md.
/// </summary>
[Collection("Console")]
public class CommandDispatcherTests
{
    [Fact]
    public async Task RunAsync_UnknownCommand_WritesNothingToStdoutAndExitsNonZero()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);

        int exitCode;
        try
        {
            exitCode = await CommandDispatcher.RunAsync(["bogus"], new NullHttpClientFactory(), CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("Unknown command: bogus", stderr.ToString());
    }

    [Fact]
    public async Task RunAsync_ListDeviceWithExtraArguments_IsRejected()
    {
        var originalError = Console.Error;
        var stderr = new StringWriter();
        Console.SetError(stderr);

        int exitCode;
        try
        {
            exitCode = await CommandDispatcher.RunAsync(
                ["list-device", "extra-arg"], new NullHttpClientFactory(), CancellationToken.None);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Equal(1, exitCode);
        Assert.Contains("Unexpected argument: extra-arg", stderr.ToString());
    }

    /// <summary>An <see cref="IHttpClientFactory"/> that never needs to actually produce a client, for command paths that exit before touching the network.</summary>
    private sealed class NullHttpClientFactory : IHttpClientFactory
    {
        /// <inheritdoc/>
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("Not expected to be called on this test's error path.");
    }
}
