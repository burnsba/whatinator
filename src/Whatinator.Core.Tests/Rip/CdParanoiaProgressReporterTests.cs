using System.Text;
using System.Text.RegularExpressions;
using Whatinator.Core.Rip;

namespace Whatinator.Core.Tests;

/// <summary>Tests for <see cref="CdParanoiaProgressReporter"/>.</summary>
public partial class CdParanoiaProgressReporterTests
{
    private const int WordsPerFrame = 588 * 2;

    [Fact]
    public void FormatField_PadsShortLabelToTwoCharacters()
    {
        Assert.Equal("[ r@     12]", CdParanoiaProgressReporter.FormatField("r", 12));
    }

    [Fact]
    public void FormatField_DoesNotPadTwoCharacterLabels()
    {
        Assert.Equal("[sc@     12]", CdParanoiaProgressReporter.FormatField("sc", 12));
    }

    [Fact]
    public void FormatField_DoesNotTruncate_WhenCountExceedsWidth()
    {
        Assert.Equal("[ r@1234567890]", CdParanoiaProgressReporter.FormatField("r", 1234567890));
    }

    [Fact]
    public void FormatElapsed_OmitsHours_WhenZero()
    {
        Assert.Equal("7:12", CdParanoiaProgressReporter.FormatElapsed(TimeSpan.FromSeconds((7 * 60) + 12)));
    }

    [Fact]
    public void FormatElapsed_IncludesHours_WhenNonZero()
    {
        Assert.Equal("1:02:03", CdParanoiaProgressReporter.FormatElapsed(new TimeSpan(1, 2, 3)));
    }

    [Fact]
    public void Percent_IsZero_BeforeAnyFeed()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        Assert.Equal(0, reporter.Percent);
    }

    [Fact]
    public void Feed_AdvancesPercent_OnFrameAlignedReadLine()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.Feed($"##: 0 [read] @ {50 * WordsPerFrame}");

        Assert.Equal(50, reporter.Percent);
    }

    [Fact]
    public void Feed_AdvancesPercent_OnFrameAlignedVerifyLine()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.Feed($"##: 1 [verify] @ {25 * WordsPerFrame}");

        Assert.Equal(25, reporter.Percent);
    }

    [Fact]
    public void Feed_IgnoresMisalignedWordOffsets()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.Feed($"##: 0 [read] @ {(50 * WordsPerFrame) + 1}");

        Assert.Equal(0, reporter.Percent);
    }

    [Fact]
    public void Feed_IgnoresNonReadVerifyFunctions()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.Feed($"##: 14 [wrote] @ {50 * WordsPerFrame}");

        Assert.Equal(0, reporter.Percent);
    }

    [Fact]
    public void Feed_IgnoresNonProgressLines()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.Feed("Ripping from sector      32 (track  1 [0:00.00])");

        Assert.Equal(0, reporter.Percent);
    }

    [Fact]
    public void Percent_IsClamped_WhenFurthestFrameExceedsTrackLength()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 9);

        reporter.Feed($"##: 0 [read] @ {20 * WordsPerFrame}");

        Assert.Equal(100, reporter.Percent);
    }

    [Fact]
    public void Feed_NormalizesAbsoluteWordOffset_ToTrackRelativeFrame_WhenStartFrameIsNonZero()
    {
        // cd-paranoia's own "##: ... @ <wordOffset>" lines are reported
        // against the whole disc, not relative to the requested track, even
        // for a track-relative read request -- see BeginRead's own doc
        // comment and root CLAUDE.md § Gotchas. Bug: docs/plan/stretch/bug-progress-percent.md.
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        const int startFrame = 16747;
        reporter.BeginRead(stopFrame: 99, startFrame: startFrame);

        reporter.Feed($"##: 0 [read] @ {(startFrame + 50) * WordsPerFrame}");

        Assert.Equal(50, reporter.Percent);
    }

    [Fact]
    public void Percent_IsClamped_WhenAbsoluteWordOffsetOverflowsTrackRange_WithNonZeroStartFrame()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        const int startFrame = 16747;
        reporter.BeginRead(stopFrame: 9, startFrame: startFrame);

        reporter.Feed($"##: 0 [read] @ {(startFrame + 20) * WordsPerFrame}");

        Assert.Equal(100, reporter.Percent);
    }

    [Fact]
    public void BeginRead_ResetsPercentAndCounts_ForANewSubRead()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99, readNumber: 1, totalReads: 2);
        reporter.Feed($"##: 0 [read] @ {99 * WordsPerFrame}");
        reporter.Complete();

        reporter.BeginRead(stopFrame: 99, readNumber: 2, totalReads: 2);

        Assert.Equal(0, reporter.Percent);
    }

    [Fact]
    public void Feed_DoesNotPrintAStatusLine_ImmediatelyAfterBeginRead()
    {
        // The 20-second throttle means a single Feed() call right after
        // BeginRead -- with no real time elapsed -- must not print anything.
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99999);

        reporter.Feed($"##: 0 [read] @ {50 * WordsPerFrame}");

        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void RelayLine_EchoesRippingBanner_WithBlankLineSpacingAndReadAnnouncement()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99, readNumber: 1, totalReads: 2);

        reporter.RelayLine("Ripping from sector      32 (track  1 [0:00.00])");
        reporter.RelayLine("\t  to sector   16746 (track  1 [3:42.64])");

        var lines = StripTimestamps(stream);

        Assert.Equal(
            [
                string.Empty,
                "Ripping from sector      32 (track  1 [0:00.00])",
                "\t  to sector   16746 (track  1 [3:42.64])",
                string.Empty,
                "Read 1 of 2",
            ],
            lines);
    }

    [Fact]
    public void RelayLine_OmitsReadAnnouncement_WhenOnlyOneRead()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.RelayLine("Ripping from sector      32 (track  1 [0:00.00])");
        reporter.RelayLine("\t  to sector   16746 (track  1 [3:42.64])");

        var lines = StripTimestamps(stream);

        Assert.DoesNotContain(lines, l => l.StartsWith("Read ", StringComparison.Ordinal));
    }

    [Fact]
    public void RelayLine_WritesUnrecognizedLines_Immediately()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.RelayLine("Unable to open device: permission denied");

        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("Unable to open device: permission denied", text);
    }

    [Fact]
    public void Complete_PrintsLegendAndCountRows()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99, readNumber: 1, totalReads: 2);
        reporter.Feed($"##: 0 [read] @ {WordsPerFrame}");
        reporter.Feed($"##: 14 [wrote] @ {WordsPerFrame}");

        reporter.Complete();

        var lines = StripTimestamps(stream);
        Assert.Contains(string.Join(", ", CdParanoiaProgressReporter.Functions), lines);
        Assert.Contains(lines, l => l.Contains("[ r@      1]", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("[ w@      1]", StringComparison.Ordinal));
    }

    [Fact]
    public void Complete_PrintsFinishedLine_WithReadNumbering_WhenMultipleReads()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99, readNumber: 2, totalReads: 2);

        reporter.Complete();

        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Matches(FinishedLinePattern("read 2 of 2"), text);
    }

    [Fact]
    public void Complete_PrintsFinishedLine_WithoutReadNumbering_WhenOnlyOneRead()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.Complete();

        var text = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Matches(FinishedLinePattern("read"), text);
    }

    [Fact]
    public void Complete_EndsWithABlankLine()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.Complete();

        var lines = StripTimestamps(stream);
        Assert.Equal(string.Empty, lines[^1]);
    }

    [Fact]
    public void EveryLine_IsPrefixedWithATimestamp()
    {
        using var stream = new MemoryStream();
        var reporter = new CdParanoiaProgressReporter(stream);
        reporter.BeginRead(stopFrame: 99);

        reporter.Complete();

        var lines = Encoding.UTF8.GetString(stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.All(lines, l => Assert.Matches(TimestampPrefixPattern(), l));
    }

    private static string[] StripTimestamps(MemoryStream stream) =>
        Encoding.UTF8.GetString(stream.ToArray())
            .Split('\n')[..^1] // drop the trailing empty element after the final '\n'
            .Select(l => TimestampPrefixPattern().Replace(l, string.Empty))
            .ToArray();

    private static Regex FinishedLinePattern(string readLabel) =>
        new(Regex.Escape(readLabel) + @" finished in \d+:\d\d");

    [GeneratedRegex(@"^\d{8}-\d{6}: ")]
    private static partial Regex TimestampPrefixPattern();
}
