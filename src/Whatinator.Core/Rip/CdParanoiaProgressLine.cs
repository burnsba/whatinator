using System.Globalization;
using System.Text.RegularExpressions;

namespace Whatinator.Core.Rip;

/// <summary>
/// Parses a single cd-paranoia <c>--stderr-progress</c> line, e.g.
/// <c>##: 0 [read] @ 57624</c> -- the shared wire-format parser behind both
/// <see cref="CdParanoiaTrackReader.ComputeQuality"/>'s post-hoc parse and
/// <see cref="CdParanoiaProgressReporter"/>'s live parse, so the regex only
/// exists once.
/// </summary>
internal static partial class CdParanoiaProgressLine
{
    /// <summary>Two 16-bit stereo samples (4 bytes) per audio sample-frame, 588 sample-frames per CD frame.</summary>
    internal const int WordsPerFrame = 588 * 2;

    /// <summary>Attempts to parse one line of cd-paranoia's <c>--stderr-progress</c> output.</summary>
    /// <param name="line">A single line, without its trailing newline.</param>
    /// <param name="function">The event's function name (e.g. <c>read</c>, <c>wrote</c>, <c>verify</c>, <c>finished</c>), or <see cref="string.Empty"/> if unmatched.</param>
    /// <param name="wordOffset">The event's 16-bit-word offset into the track, or <c>0</c> if unmatched.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="line"/> matched the <c>##:</c>
    /// wire format and its offset digits fit an <see cref="int"/>; <see langword="false"/>
    /// otherwise, including a captured offset too large to parse (this is
    /// untrusted text scraped from another program's stderr, which may be
    /// garbled or truncated -- see root <c>CLAUDE.md</c> § Gotchas).
    /// </returns>
    public static bool TryParse(string line, out string function, out int wordOffset)
    {
        var match = ProgressLinePattern().Match(line);
        if (!match.Success)
        {
            function = string.Empty;
            wordOffset = 0;
            return false;
        }

        if (!int.TryParse(match.Groups["offset"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out wordOffset))
        {
            function = string.Empty;
            wordOffset = 0;
            return false;
        }

        function = match.Groups["function"].Value;
        return true;
    }

    /// <summary>
    /// Converts one <c>##:</c> line's <paramref name="wordOffset"/> -- which
    /// cd-paranoia reports as an absolute disc-frame offset, not
    /// track-relative, even though the requested read range is
    /// track-relative (confirmed live against a real drive; see
    /// <see cref="CdParanoiaProgressReporter.BeginRead"/> and root
    /// <c>CLAUDE.md</c> § Gotchas) -- into a frame offset relative to
    /// <paramref name="trackStartFrame"/>. Shared by
    /// <see cref="CdParanoiaProgressReporter.Feed"/>'s live parse and
    /// <see cref="CdParanoiaTrackReader.ComputeQuality"/>'s post-hoc parse so
    /// the two conversions cannot diverge again.
    /// </summary>
    /// <param name="wordOffset">The line's parsed 16-bit-word offset, as returned by <see cref="TryParse"/>.</param>
    /// <param name="trackStartFrame">The track's own absolute start frame, in the same disc-frame numbering as <paramref name="wordOffset"/>.</param>
    /// <returns>The frame offset relative to the track's start.</returns>
    internal static int ToTrackRelativeFrame(int wordOffset, int trackStartFrame) =>
        (wordOffset / WordsPerFrame) - trackStartFrame;

    /// <summary>Matches a cd-paranoia <c>--stderr-progress</c> line, e.g. <c>##: 0 [read] @ 57624</c>.</summary>
    [GeneratedRegex(@"^##: (?<code>.+)\s\[(?<function>.*)\]\s@\s(?<offset>\d+)")]
    private static partial Regex ProgressLinePattern();
}
