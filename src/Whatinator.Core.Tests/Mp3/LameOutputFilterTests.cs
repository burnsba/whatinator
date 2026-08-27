using Whatinator.Core.Mp3;

namespace Whatinator.Core.Tests;

public class LameOutputFilterTests
{
    private const string Esc = "\x1B";

    [Fact]
    public void ExtractSummary_StripsAnsiEscapeCodes()
    {
        var raw = "LAME 4.0 64bits\n"
            + " 32 [0] " + Esc + "[K\n"
            + $" 64 [4889] {Esc}[K\n"
            + "-------------------------------------------------------------------------------" + Esc + "[K\n"
            + "   kbps        MS  %     long switch short %" + Esc + "[K\n"
            + "   61.7      100.0       100.0   0.0   0.0" + Esc + "[K\n"
            + "Writing LAME Tag...done\n"
            + "ReplayGain: -11.8dB\n";

        var summary = LameOutputFilter.ExtractSummary(raw);

        Assert.DoesNotContain(Esc, summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractSummary_KeepsOnlyTheLastHistogramRedraw()
    {
        // Modeled on a real captured `lame -V0` stderr: the bitrate
        // histogram + separator + kbps line is redrawn in place (via \r
        // plus ANSI cursor-up codes) every progress update, with only
        // "Writing LAME Tag...done"/"ReplayGain: ..." appearing once, at
        // the very end.
        var firstRedraw =
            " 32 [0] " + Esc + "[K\r"
            + " 64 [0] " + Esc + "[K\r"
            + "-------------------------------------------------------------------------------" + Esc + "[K\r"
            + "   kbps        MS  %     long switch short %" + Esc + "[K\r"
            + "    0.0        0.0         0.0   0.0   0.0" + Esc + "[K"
            + Esc + "[A" + Esc + "[A" + Esc + "[A" + Esc + "[A"
            + "\r   500/6892     ( 7%)|    0:00/    0:01|    0:00/    0:01|   137.33x|    0:00 \n";

        var finalRedraw =
            " 32 [   0] " + Esc + "[K\n"
            + " 64 [4889] " + Esc + "[K\n"
            + "-------------------------------------------------------------------------------" + Esc + "[K\n"
            + "   kbps        MS  %     long switch short %" + Esc + "[K\n"
            + "   61.7      100.0       100.0   0.0   0.0" + Esc + "[K\n"
            + "Writing LAME Tag...done\n"
            + "ReplayGain: -11.8dB\n";

        var raw = "LAME 4.0 64bits (https://lame.sourceforge.io)\n"
            + "Encoding test.wav to test.mp3\n"
            + firstRedraw
            + firstRedraw
            + finalRedraw;

        var summary = LameOutputFilter.ExtractSummary(raw);

        Assert.DoesNotContain("LAME 4.0", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("500/6892", summary, StringComparison.Ordinal);

        // Only one copy of the histogram/summary block should remain.
        Assert.Equal(1, CountOccurrences(summary, "kbps        MS"));

        Assert.StartsWith("32 [   0]", summary, StringComparison.Ordinal);
        Assert.EndsWith("ReplayGain: -11.8dB", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractSummary_FailsOpen_WhenHistogramRowNotFound()
    {
        const string raw = "some unexpected lame output format\nwith no histogram at all\n";

        var summary = LameOutputFilter.ExtractSummary(raw);

        Assert.Equal(raw.Trim(), summary);
    }

    [Fact]
    public void ExtractSummary_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, LameOutputFilter.ExtractSummary(string.Empty));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
