using System.Text.RegularExpressions;

namespace Whatinator.Core.Toc;

/// <summary>
/// Filters <c>cdrdao read-toc</c>'s live stderr output for
/// <see cref="CdrdaoTocReader"/>. cdrdao's full-scan mode re-announces every
/// track's start/length a second time via "Analyzing track NN (AUDIO):
/// start X, length Y..." lines -- redundant once the earlier Track/Mode/
/// Flags/Start/Length table has already shown the same numbers -- so those
/// lines are suppressed. cdrdao also never prints the catalogue number's
/// actual value on stderr, only "Found disk catalogue number." -- that line
/// is suppressed here too, tracked via <see cref="SawCatalogLine"/> so
/// <see cref="CdrdaoTocReader"/> can re-emit it with the value once the
/// parsed <c>.toc</c> file (the only place the value exists) is available.
/// Also suppressed: the disc-capability banner cdrdao prints once at startup
/// ("PQ sub-channel reading ... is supported", "Raw P-W sub-channel
/// reading...", "Cooked R-W sub-channel reading...") -- purely informational
/// and not something whatinator's output format ever surfaced -- and the
/// bare <c>MM:SS:FF</c> elapsed-time progress lines cdrdao prints once per
/// second while scanning a track's pregap during a full (non-fast) TOC read.
/// Unlike cd-paranoia's <c>##:</c> progress (see
/// <see cref="Rip.CdParanoiaProgressReporter"/>), nothing downstream
/// consumes or reformats these, so they're dropped outright rather than
/// rewritten. Every other line passes through unchanged.
/// </summary>
internal sealed partial class CdrdaoLiveOutputFilter
{
    private const string CatalogLine = "Found disk catalogue number.";

    /// <summary>Whether <see cref="Process"/> has suppressed the catalogue-number line yet.</summary>
    public bool SawCatalogLine { get; private set; }

    /// <summary>Filters one line of cdrdao's stderr output.</summary>
    /// <param name="line">A single line, without its trailing newline.</param>
    /// <returns>The line to relay, or <see langword="null"/> to suppress it.</returns>
    public string? Process(string line)
    {
        if (line == CatalogLine)
        {
            SawCatalogLine = true;
            return null;
        }

        if (AnalyzingTrackLine().IsMatch(line) || SubChannelCapabilityLine().IsMatch(line) || ElapsedTimeProgressLine().IsMatch(line))
        {
            return null;
        }

        return line;
    }

    /// <summary>Matches e.g. <c>Analyzing track 01 (AUDIO): start 00:00:32, length 03:42:65...</c>.</summary>
    [GeneratedRegex(@"^Analyzing track \d+ \(AUDIO\): start .*, length .*\.\.\.$")]
    private static partial Regex AnalyzingTrackLine();

    /// <summary>Matches the one-time "PQ/Raw P-W/Cooked R-W sub-channel reading ... is supported" capability banner.</summary>
    [GeneratedRegex(@"^(PQ|Raw P-W|Cooked R-W) sub-channel reading \(audio track\) is supported\b.*\.$")]
    private static partial Regex SubChannelCapabilityLine();

    /// <summary>Matches a bare <c>MM:SS:FF</c> pregap-scan progress line, e.g. <c>00:01:00</c>.</summary>
    [GeneratedRegex(@"^\d{2}:\d{2}:\d{2}$")]
    private static partial Regex ElapsedTimeProgressLine();
}
