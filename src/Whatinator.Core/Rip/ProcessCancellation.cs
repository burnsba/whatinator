using System.Diagnostics;

namespace Whatinator.Core.Rip;

/// <summary>
/// Kills a subprocess's whole process tree when cancellation fires while
/// waiting on it, shared by every subprocess wrapper in this project
/// (<see cref="CdParanoiaTrackReader"/>, <see cref="Toc.CdrdaoTocReader"/>,
/// <see cref="Flac.FlacEncoder"/>, <see cref="Mp3.LameEncoder"/>,
/// <see cref="Drive.CacheDefeatAnalyzer"/>). Without this, a plain
/// <c>await process.WaitForExitAsync(cancellationToken)</c> just throws on
/// cancel -- the child keeps running, holding the drive or a scratch file
/// open (see root <c>CLAUDE.md</c> § Gotchas).
/// </summary>
internal static class ProcessCancellation
{
    /// <summary>
    /// Waits for <paramref name="process"/> to exit, killing its entire
    /// process tree and rethrowing if <paramref name="cancellationToken"/>
    /// fires first.
    /// </summary>
    /// <param name="process">The already-started process to wait on.</param>
    /// <param name="cancellationToken">The token that cancels the wait.</param>
    /// <returns>A task that completes once <paramref name="process"/> exits.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    internal static async Task WaitForExitOrKillAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited between the cancellation firing and Kill() being called.
            }

            throw;
        }
    }
}
