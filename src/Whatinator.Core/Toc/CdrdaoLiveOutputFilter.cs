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
/// Every other line passes through unchanged.
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

        return AnalyzingTrackLine().IsMatch(line) ? null : line;
    }

    /// <summary>Matches e.g. <c>Analyzing track 01 (AUDIO): start 00:00:32, length 03:42:65...</c>.</summary>
    [GeneratedRegex(@"^Analyzing track \d+ \(AUDIO\): start .*, length .*\.\.\.$")]
    private static partial Regex AnalyzingTrackLine();
}
