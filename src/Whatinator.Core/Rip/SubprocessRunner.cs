using System.Diagnostics;

namespace Whatinator.Core.Rip;

/// <summary>
/// Starts a subprocess, drains its stdout/stderr concurrently, and waits for
/// it to exit with kill-on-cancel (via <see cref="ProcessCancellation"/>) --
/// the sequence every subprocess wrapper in this project needs
/// (<see cref="CdParanoiaTrackReader"/>, <see cref="Toc.CdrdaoTocReader"/>,
/// <see cref="Flac.FlacEncoder"/>, <see cref="Mp3.LameEncoder"/>,
/// <see cref="Drive.CacheDefeatAnalyzer"/>, <see cref="CoverArt.CoverArtProcessor"/>).
/// Each caller drains its two streams differently -- a live byte relay, a
/// line-filtered relay, a full-text capture, or a plain drain to
/// <see cref="Stream.Null"/> -- so the streams are handed to caller-supplied
/// delegates rather than this class picking one drain strategy. Don't switch
/// this to a <c>WaitForExit</c>-then-read shape: an undrained pipe can fill
/// and deadlock a chatty tool like <c>cd-paranoia</c> or <c>magick</c> (see
/// root <c>CLAUDE.md</c> § Gotchas).
/// </summary>
internal static class SubprocessRunner
{
    /// <summary>Starts <paramref name="startInfo"/> and runs it to completion, draining both output streams concurrently.</summary>
    /// <param name="startInfo">The process to start. Must have <c>RedirectStandardOutput</c> and <c>RedirectStandardError</c> set.</param>
    /// <param name="handleStandardOutput">Drains the started process's stdout reader.</param>
    /// <param name="handleStandardError">Drains the started process's stderr reader.</param>
    /// <param name="cancellationToken">A token that kills the process's entire tree and cancels the wait if fired before the process exits.</param>
    /// <returns>The process's exit code.</returns>
    internal static async Task<int> RunAsync(
        ProcessStartInfo startInfo,
        Func<StreamReader, CancellationToken, Task> handleStandardOutput,
        Func<StreamReader, CancellationToken, Task> handleStandardError,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = handleStandardOutput(process.StandardOutput, cancellationToken);
        var stderrTask = handleStandardError(process.StandardError, cancellationToken);

        await ProcessCancellation.WaitForExitOrKillAsync(process, cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

        return process.ExitCode;
    }
}
