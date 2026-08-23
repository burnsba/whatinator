namespace Whatinator.Core;

/// <summary>
/// Converts an unhandled exception escaping a CLI entry point into a one-line
/// stderr message and a non-zero exit code, instead of a raw stack trace. The
/// logic lives here rather than in <c>Whatinator.Cli</c>'s <c>Program.cs</c>
/// because that project has no test project by design (see root
/// <c>CLAUDE.md</c>) -- this is the testable seam <c>Program.cs</c> delegates
/// to.
/// </summary>
public static class CliExceptionBoundary
{
    /// <summary>The exit code returned when the run action throws and isn't rethrown.</summary>
    public const int UnhandledExceptionExitCode = 1;

    /// <summary>
    /// Invokes <paramref name="action"/> and returns its result. An
    /// <see cref="OperationCanceledException"/> always propagates -- the
    /// caller's own Ctrl-C handling (see <c>Program.cs</c>) owns that case.
    /// Any other exception is either rethrown untouched (when
    /// <paramref name="showStackTrace"/> is set, e.g. via <c>--debug</c>) or
    /// reported as <see cref="Exception.Message"/> written to
    /// <paramref name="errorWriter"/>, with <see cref="UnhandledExceptionExitCode"/>
    /// returned instead of propagating.
    /// </summary>
    /// <param name="action">The CLI entry point to run.</param>
    /// <param name="errorWriter">Where to write a caught exception's message.</param>
    /// <param name="showStackTrace">
    /// When <see langword="true"/>, exceptions other than
    /// <see cref="OperationCanceledException"/> are rethrown instead of
    /// caught, so the runtime's default handler prints the full trace.
    /// </param>
    /// <returns>
    /// <paramref name="action"/>'s result, or <see cref="UnhandledExceptionExitCode"/>
    /// if it threw and <paramref name="showStackTrace"/> was <see langword="false"/>.
    /// </returns>
    public static async Task<int> RunAsync(Func<Task<int>> action, TextWriter errorWriter, bool showStackTrace)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(errorWriter);

        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (showStackTrace)
        {
            throw;
        }
        catch (Exception ex)
        {
            errorWriter.WriteLine(ex.Message);
            return UnhandledExceptionExitCode;
        }
    }
}
