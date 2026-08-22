using Whatinator.Core.Mp3;

namespace Whatinator.Core.Rip;

/// <summary>
/// Relays a subprocess's output streams live, without line buffering.
/// Shared by <see cref="LameEncoder"/> and <see cref="Flac.FlacEncoder"/> --
/// lame's/flac's progress output uses carriage-return redraws rather than
/// newlines, so a line-buffered relay (e.g. .NET's
/// <c>Process.OutputDataReceived</c>, which waits for <c>\n</c>) would hide
/// progress until the next real newline. <see cref="Toc.CdrdaoTocReader"/>
/// switched to its own line-filtered relay instead (see
/// <see cref="Toc.CdrdaoLiveOutputFilter"/>) since cdrdao's own output is
/// plain newline-terminated lines, not <c>\r</c>-redrawn.
/// <see cref="CdParanoiaTrackReader"/> needs its own line-level tee variant
/// instead of this one too (it also parses the captured text for track
/// quality afterward, and feeds its own <c>##:</c> lines to
/// <see cref="CdParanoiaProgressReporter"/> rather than relaying them
/// straight through), see its own remarks.
/// </summary>
internal static class ProcessOutputRelay
{
    /// <summary>The size, in bytes, of the buffer used to relay a process's output streams.</summary>
    private const int BufferSize = 4096;

    /// <summary>Relays raw bytes from <paramref name="source"/> to <paramref name="destination"/> as they arrive.</summary>
    /// <param name="source">The stream to read from.</param>
    /// <param name="destination">The stream to write to.</param>
    /// <param name="cancellationToken">A token to cancel the relay.</param>
    /// <param name="capture">An optional second destination every relayed byte is also mirrored into (e.g. so a caller can capture the subprocess's output for a log, in addition to relaying it live).</param>
    /// <returns>A task that completes once <paramref name="source"/> reaches end of stream.</returns>
    internal static async Task RelayAsync(Stream source, Stream destination, CancellationToken cancellationToken, Stream? capture = null)
    {
        var buffer = new byte[BufferSize];
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (capture is not null)
            {
                await capture.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
