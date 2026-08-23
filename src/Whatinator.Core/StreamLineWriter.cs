using System.Text;
using Whatinator.Core.Rip;

namespace Whatinator.Core;

/// <summary>
/// Writes a single newline-terminated line to a live progress/log
/// <see cref="Stream"/>, flushing immediately so it stays live for whatever
/// is reading the other end. Shared by every subprocess-driving class that
/// relays its own status lines (as opposed to a subprocess's own output,
/// which is relayed through <see cref="Whatinator.Core.Rip.ProcessOutputRelay"/>
/// instead).
/// </summary>
internal static class StreamLineWriter
{
    /// <summary>Writes a single line to <paramref name="stream"/>.</summary>
    /// <param name="stream">The stream to write into.</param>
    /// <param name="message">The line's text, without a trailing newline.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <param name="timestamped">
    /// Whether to prefix the line with <see cref="RipOutputTimestamp.Prefix"/>
    /// -- only true once a rip is underway (see root <c>CLAUDE.md</c> §
    /// Gotchas: "Console output prefixing").
    /// </param>
    /// <returns>A task that completes once the line has been written and flushed.</returns>
    public static async Task WriteLineAsync(Stream stream, string message, CancellationToken cancellationToken, bool timestamped = false)
    {
        var text = timestamped ? RipOutputTimestamp.Prefix() + message : message;
        var bytes = Encoding.UTF8.GetBytes(text + Environment.NewLine);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
