using System.Text.RegularExpressions;

namespace Whatinator.Core.Rip;

/// <summary>
/// Filters cd-paranoia's live stderr for <see cref="CdParanoiaTrackReader"/>:
/// suppresses its startup banner/preamble (tool name/version, copyright
/// lines, "Report bugs...", "outputting to...", blank lines) and its own
/// bare "Done." completion line -- routine boilerplate
/// <see cref="CdParanoiaProgressReporter"/>'s own final event-count summary
/// already makes redundant. Deliberately does <em>not</em> suppress the
/// "Ripping from sector.../to sector..." pair -- that's cd-paranoia's own
/// real announcement of the sector span it's about to read, and
/// <see cref="CdParanoiaProgressReporter.RelayLine"/> echoes it (with its
/// own blank-line spacing and a "Read N of M" line) rather than
/// reformatting the numbers itself. <c>##:</c> progress lines and anything
/// else unrecognized (e.g. a genuine error) pass through unchanged --
/// fail-open, same intent as <see cref="Toc.CdrdaoLiveOutputFilter"/>.
/// </summary>
internal static partial class CdParanoiaLiveOutputFilter
{
    /// <summary>Filters one line of cd-paranoia's stderr output.</summary>
    /// <param name="line">A single line, without its trailing newline.</param>
    /// <returns>The line to relay, or <see langword="null"/> to suppress it.</returns>
    public static string? Process(string line)
    {
        if (line.Length == 0
            || line == "Sending all callback output to stderr for wrapper script"
            || line == "Report bugs to bug-libcdio@gnu.org"
            || line == "Done.")
        {
            return null;
        }

        return BoilerplatePattern().IsMatch(line) ? null : line;
    }

    /// <summary>Matches the tool/version banner, copyright lines, and the "outputting to..." announcement.</summary>
    [GeneratedRegex(@"^(cdparanoia\b.*|\(C\)\s.*|outputting to\s.*)$")]
    private static partial Regex BoilerplatePattern();
}
