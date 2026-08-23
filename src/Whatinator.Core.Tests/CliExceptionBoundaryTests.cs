namespace Whatinator.Core.Tests;

public class CliExceptionBoundaryTests
{
    [Fact]
    public async Task RunAsync_ReturnsActionResult_WhenActionSucceeds()
    {
        var writer = new StringWriter();

        var exitCode = await CliExceptionBoundary.RunAsync(() => Task.FromResult(42), writer, showStackTrace: false);

        Assert.Equal(42, exitCode);
        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public async Task RunAsync_WritesMessageAndReturnsExitCode_WhenActionThrows()
    {
        var writer = new StringWriter();

        var exitCode = await CliExceptionBoundary.RunAsync(
            () => throw new InvalidOperationException("something went wrong"),
            writer,
            showStackTrace: false);

        Assert.Equal(CliExceptionBoundary.UnhandledExceptionExitCode, exitCode);
        Assert.Equal("something went wrong" + writer.NewLine, writer.ToString());
    }

    [Fact]
    public async Task RunAsync_Rethrows_WhenShowStackTraceIsTrue()
    {
        var writer = new StringWriter();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CliExceptionBoundary.RunAsync(
            () => throw new InvalidOperationException("boom"),
            writer,
            showStackTrace: true));

        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public async Task RunAsync_AlwaysRethrows_OperationCanceledException()
    {
        var writer = new StringWriter();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CliExceptionBoundary.RunAsync(
            () => throw new OperationCanceledException(),
            writer,
            showStackTrace: false));

        Assert.Equal(string.Empty, writer.ToString());
    }
}
