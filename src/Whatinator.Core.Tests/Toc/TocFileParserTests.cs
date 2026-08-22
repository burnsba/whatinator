using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class TocFileParserTests
{
    // Captured live via `cdrdao read-toc --fast-toc --device /dev/sr1` against
    // a real 11-track audio CD. Catalog/ISRC values are redacted placeholders
    // -- the frame-position numbers are the real, unmodified spike output (see
    // docs/plan/implementation/phase-013.md § Research findings).
    private const string FastTocFixture = """
        CD_DA

        CATALOG "0000000000000"

        // Track 1
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000001"
        SILENCE 00:00:32
        FILE "data.wav" 0 03:42:65
        START 00:00:32


        // Track 2
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000002"
        FILE "data.wav" 03:42:65 03:48:05


        // Track 3
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000003"
        FILE "data.wav" 07:30:70 03:56:18


        // Track 4
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000004"
        FILE "data.wav" 11:27:13 04:03:22


        // Track 5
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000005"
        FILE "data.wav" 15:30:35 03:53:63


        // Track 6
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000006"
        FILE "data.wav" 19:24:23 04:58:17


        // Track 7
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000007"
        FILE "data.wav" 24:22:40 02:29:63


        // Track 8
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000008"
        FILE "data.wav" 26:52:28 03:28:32


        // Track 9
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000009"
        FILE "data.wav" 30:20:60 08:25:33


        // Track 10
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000010"
        FILE "data.wav" 38:46:18 04:44:55


        // Track 11
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000011"
        FILE "data.wav" 43:30:73 04:19:02
        """;

    // Captured live via `cdrdao read-toc --device /dev/sr1` (no --fast-toc)
    // against the same disc as FastTocFixture -- same redaction.
    private const string FullTocFixture = """
        CD_DA

        CATALOG "0000000000000"

        // Track 1
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000001"
        SILENCE 00:00:32
        FILE "data.wav" 0 03:41:00
        START 00:00:32


        // Track 2
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000002"
        FILE "data.wav" 03:41:00 03:48:48
        START 00:01:65


        // Track 3
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000003"
        FILE "data.wav" 07:29:48 03:57:00
        START 00:01:22


        // Track 4
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000004"
        FILE "data.wav" 11:26:48 04:02:57
        START 00:00:40


        // Track 5
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000005"
        FILE "data.wav" 15:29:30 03:53:70
        START 00:01:05


        // Track 6
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000006"
        FILE "data.wav" 19:23:25 04:57:60
        START 00:00:73


        // Track 7
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000007"
        FILE "data.wav" 24:21:10 02:31:18
        START 00:01:30


        // Track 8
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000008"
        FILE "data.wav" 26:52:28 03:28:02


        // Track 9
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000009"
        FILE "data.wav" 30:20:30 08:25:63
        START 00:00:30


        // Track 10
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000010"
        FILE "data.wav" 38:46:18 04:43:02


        // Track 11
        TRACK AUDIO
        NO COPY
        NO PRE_EMPHASIS
        TWO_CHANNEL_AUDIO
        ISRC "XXAA00000011"
        FILE "data.wav" 43:29:20 04:20:55
        START 00:01:53
        """;

    [Fact]
    public void Parse_FastToc_ReturnsAllElevenTracksAsAudio()
    {
        var toc = TocFileParser.Parse(FastTocFixture);

        Assert.Equal(11, toc.Tracks.Count);
        Assert.All(toc.Tracks, t => Assert.True(t.IsAudio));
    }

    [Theory]
    [InlineData(1, 32, 16746)]
    [InlineData(2, 16747, 33851)]
    [InlineData(3, 33852, 51569)]
    [InlineData(9, 136592, 174499)]
    [InlineData(11, 195855, 215281)]
    public void Parse_FastToc_ComputesExpectedStartAndEndFrames(int trackNumber, int expectedStart, int expectedEnd)
    {
        var toc = TocFileParser.Parse(FastTocFixture);

        var track = toc.Tracks[trackNumber - 1];
        Assert.Equal(trackNumber, track.TrackNumber);
        Assert.Equal(expectedStart, track.StartFrame);
        Assert.Equal(expectedEnd, track.EndFrame);
    }

    [Fact]
    public void Parse_FastToc_ComputesLeadoutFrame()
    {
        var toc = TocFileParser.Parse(FastTocFixture);

        Assert.Equal(215282, toc.LeadoutFrame);
    }

    [Fact]
    public void Parse_FastToc_OnlyTrack1HasAPregap()
    {
        // --fast-toc skips index/pregap scanning for every track but the
        // first, whose leading silence is read straight off the disc's raw
        // TOC (no audio scan needed) -- see root CLAUDE.md § Gotchas.
        var toc = TocFileParser.Parse(FastTocFixture);

        Assert.Equal(32, toc.Tracks[0].PregapFrames);
        Assert.All(toc.Tracks.Skip(1), t => Assert.Null(t.PregapFrames));
    }

    [Fact]
    public void Parse_FastToc_CapturesIsrcPerTrack()
    {
        var toc = TocFileParser.Parse(FastTocFixture);

        Assert.Equal("XXAA00000001", toc.Tracks[0].Isrc);
        Assert.Equal("XXAA00000011", toc.Tracks[10].Isrc);
    }

    [Fact]
    public void Parse_CapturesCatalogNumber_WhenPresent()
    {
        var toc = TocFileParser.Parse(FastTocFixture);

        Assert.Equal("0000000000000", toc.CatalogNumber);
    }

    [Fact]
    public void Parse_CatalogNumberIsNull_WhenAbsent()
    {
        const string tocText = """
            CD_DA

            TRACK AUDIO
            NO COPY
            NO PRE_EMPHASIS
            TWO_CHANNEL_AUDIO
            FILE "data.wav" 0 00:05:00
            """;

        var toc = TocFileParser.Parse(tocText);

        Assert.Null(toc.CatalogNumber);
    }

    [Theory]
    [InlineData(1, 32)]
    [InlineData(2, 140)]
    [InlineData(3, 97)]
    [InlineData(4, 40)]
    [InlineData(5, 80)]
    [InlineData(6, 73)]
    [InlineData(7, 105)]
    [InlineData(9, 30)]
    [InlineData(11, 128)]
    public void Parse_FullToc_CapturesPerTrackPregaps(int trackNumber, int expectedPregapFrames)
    {
        var toc = TocFileParser.Parse(FullTocFixture);

        Assert.Equal(expectedPregapFrames, toc.Tracks[trackNumber - 1].PregapFrames);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(10)]
    public void Parse_FullToc_TracksWithNoDetectedGapHaveNullPregap(int trackNumber)
    {
        // cdrdao's own live output for this disc reports no "Found pre-gap"
        // line for tracks 8/10 even under a full scan -- genuinely zero gap,
        // not "not scanned".
        var toc = TocFileParser.Parse(FullTocFixture);

        Assert.Null(toc.Tracks[trackNumber - 1].PregapFrames);
    }

    [Fact]
    public void Parse_FullToc_ComputesTheSameStartAndEndFramesAsFastToc()
    {
        // cdrdao's own "Analyzing track N: start X, length Y" summary line
        // is identical between --fast-toc and a full scan for this disc --
        // only the finer per-track pregap breakdown differs. Confirmed live.
        var fast = TocFileParser.Parse(FastTocFixture);
        var full = TocFileParser.Parse(FullTocFixture);

        for (var i = 0; i < fast.Tracks.Count; i++)
        {
            Assert.Equal(fast.Tracks[i].StartFrame, full.Tracks[i].StartFrame);
            Assert.Equal(fast.Tracks[i].EndFrame, full.Tracks[i].EndFrame);
        }
    }

    [Fact]
    public void Parse_MixedModeDisc_MarksDataTrackAsNotAudio()
    {
        const string toc = """
            CD_ROM_XA

            // Track 1
            TRACK AUDIO
            NO COPY
            NO PRE_EMPHASIS
            TWO_CHANNEL_AUDIO
            FILE "data.wav" 0 00:02:00

            // Track 2
            TRACK MODE1
            DATAFILE "data.bin" 00:03:00
            """;

        var result = TocFileParser.Parse(toc);

        Assert.Equal(2, result.Tracks.Count);
        Assert.True(result.Tracks[0].IsAudio);
        Assert.False(result.Tracks[1].IsAudio);
        Assert.Equal(0, result.Tracks[0].StartFrame);
        Assert.Equal(149, result.Tracks[0].EndFrame);
        Assert.Equal(150, result.Tracks[1].StartFrame);
        Assert.Equal(374, result.Tracks[1].EndFrame);
    }

    [Fact]
    public void Parse_PregapShorthand_IsEquivalentToSilenceAndStart()
    {
        const string toc = """
            CD_DA

            // Track 1
            TRACK AUDIO
            FILE "data.wav" 0 00:02:00

            // Track 2
            TRACK AUDIO
            PREGAP 00:00:10
            FILE "data.wav" 00:02:00 00:01:00
            """;

        var result = TocFileParser.Parse(toc);

        Assert.Equal(10, result.Tracks[1].PregapFrames);

        // Track 1's EndFrame is track 2's StartFrame (index 1, post-pregap)
        // minus one, not the raw end of track 1's own FILE stanza (150) --
        // the pregap between them counts toward the previous track's range,
        // matching the project's own EndFrame convention (see root
        // CLAUDE.md § Gotchas and DiscTocTrack.EndFrame's XML doc remarks).
        Assert.Equal(159, result.Tracks[0].EndFrame);
        Assert.Equal(160, result.Tracks[1].StartFrame);
        Assert.Equal(234, result.Tracks[1].EndFrame);
    }

    [Fact]
    public void Parse_EmptyText_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => TocFileParser.Parse(string.Empty));
    }

    [Fact]
    public void Parse_UnrecognizedStatement_ThrowsFormatException()
    {
        const string toc = """
            CD_DA

            // Track 1
            TRACK AUDIO
            NONSENSE_STATEMENT foo
            FILE "data.wav" 0 00:02:00
            """;

        Assert.Throws<FormatException>(() => TocFileParser.Parse(toc));
    }

    [Fact]
    public void Parse_StatementBeforeAnyTrack_ThrowsFormatException()
    {
        const string toc = """
            CD_DA

            ISRC "XXAA00000001"

            // Track 1
            TRACK AUDIO
            FILE "data.wav" 0 00:02:00
            """;

        Assert.Throws<FormatException>(() => TocFileParser.Parse(toc));
    }

    [Fact]
    public void Parse_TruncatedFileStatement_ThrowsFormatException_NotASilentWrongAnswer()
    {
        // Simulates a real .toc file truncated mid-write (e.g. a killed
        // subprocess): the final FILE statement's length field is cut off
        // partway through -- a naive parser could silently misread this as a
        // shorter/different valid length instead of failing.
        var cutoff = FastTocFixture.LastIndexOf("04:19", StringComparison.Ordinal) + 2;
        var truncated = FastTocFixture[..cutoff];

        Assert.Throws<FormatException>(() => TocFileParser.Parse(truncated));
    }

    [Fact]
    public void Parse_FileStatementWithOmittedLength_ThrowsFormatException()
    {
        const string toc = """
            CD_DA

            // Track 1
            TRACK AUDIO
            FILE "data.wav" 0
            """;

        Assert.Throws<FormatException>(() => TocFileParser.Parse(toc));
    }
}
