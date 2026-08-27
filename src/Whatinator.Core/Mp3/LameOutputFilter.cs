using System.Text.RegularExpressions;

namespace Whatinator.Core.Mp3;

/// <summary>
/// Filters <c>lame</c>'s raw captured stderr down to the one-time final
/// summary <see cref="Mp3LogFile"/> wants, discarding its live progress
/// display. Confirmed live: <c>lame</c> redraws its progress in place using
/// <c>\r</c> plus ANSI cursor-movement/erase codes (<c>ESC[K</c>,
/// <c>ESC[A</c>), reprinting its whole bitrate-distribution histogram and
/// <c>kbps/MS/%</c> summary line on every redraw -- captured verbatim (no
/// real terminal to interpret the codes), that's a large amount of
/// duplicated, control-character-laden noise per track. Only the version
/// banner and initial "Encoding ... to ..." announcement are genuinely
/// one-time <em>before</em> the histogram; <c>Writing LAME Tag...done</c>
/// and <c>ReplayGain: ...</c> are genuinely one-time <em>after</em> it,
/// appended once encoding finishes and never redrawn.
/// </summary>
internal static partial class LameOutputFilter
{
    /// <summary>Matches an ANSI CSI escape sequence (0x1B + <c>[</c> + params + a final letter), e.g. <c>ESC[K</c> or <c>ESC[A</c>.</summary>
    private const string AnsiEscapePatternText = "\x1B\\[[0-9;]*[A-Za-z]";

    /// <summary>
    /// Extracts the final summary block from <paramref name="rawOutput"/>:
    /// the last copy of the bitrate-distribution histogram (rows starting
    /// with <c>32 [</c> -- MPEG-1 Layer III's lowest bitrate, always the
    /// first histogram row lame prints under the <c>-V0</c> mode
    /// <see cref="LameEncoder.BuildStartInfo"/> always uses) through the end
    /// of output, i.e. the separator line, the <c>kbps/MS/%</c> line,
    /// <c>Writing LAME Tag...done</c>, and <c>ReplayGain: ...</c>.
    /// </summary>
    /// <param name="rawOutput">lame's raw captured stderr for one track.</param>
    /// <returns>
    /// The final summary block, trimmed. Fails open to the full
    /// ANSI-stripped/newline-normalized text if the histogram's first row
    /// can't be found (an unexpected lame output format) rather than
    /// discarding everything.
    /// </returns>
    public static string ExtractSummary(string rawOutput)
    {
        ArgumentNullException.ThrowIfNull(rawOutput);

        var cleaned = AnsiEscapePattern().Replace(rawOutput, string.Empty).Replace('\r', '\n');
        var lines = cleaned.Split('\n');

        var histogramStart = -1;
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (HistogramFirstRowPattern().IsMatch(lines[i]))
            {
                histogramStart = i;
                break;
            }
        }

        var kept = histogramStart >= 0 ? lines[histogramStart..] : lines;
        return string.Join('\n', kept).Trim();
    }

    [GeneratedRegex(AnsiEscapePatternText)]
    private static partial Regex AnsiEscapePattern();

    /// <summary>Matches the bitrate-distribution histogram's first row (32 kbps).</summary>
    [GeneratedRegex(@"^\s*32\s*\[")]
    private static partial Regex HistogramFirstRowPattern();
}
