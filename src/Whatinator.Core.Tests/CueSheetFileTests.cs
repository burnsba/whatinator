using Whatinator.Core.Metadata;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class CueSheetFileTests
{
    [Fact]
    public void Format_SingleDisc_WritesCatalogPerformerTitleAndPerTrackData()
    {
        var releaseInfo = CreateReleaseInfo("Artist", "Album");
        var tracks = new (TrackInfo, string)[]
        {
            (new TrackInfo(1, "Track One", "Artist", TimeSpan.FromSeconds(100)), "01 - Track One.flac"),
            (new TrackInfo(2, "Track Two", "Artist", TimeSpan.FromSeconds(101)), "02 - Track Two.flac"),
        };
        var toc = new DiscToc(
            [
                new DiscTocTrack(1, 0, 7499, IsAudio: true, PregapFrames: null, Isrc: "USRC17607839"),
                new DiscTocTrack(2, 7500, 14999, IsAudio: true, PregapFrames: null, Isrc: "USRC17607840"),
            ],
            CatalogNumber: "602475160991");

        var text = CueSheetFile.Format(releaseInfo, tracks, toc);

        Assert.Contains("CATALOG 602475160991\n", text, StringComparison.Ordinal);
        Assert.Contains("PERFORMER \"Artist\"\n", text, StringComparison.Ordinal);
        Assert.Contains("TITLE \"Album\"\n", text, StringComparison.Ordinal);
        Assert.Contains("FILE \"01 - Track One.flac\" WAVE\n", text, StringComparison.Ordinal);
        Assert.Contains("  TRACK 01 AUDIO\n", text, StringComparison.Ordinal);
        Assert.Contains("    TITLE \"Track One\"\n", text, StringComparison.Ordinal);
        Assert.Contains("    PERFORMER \"Artist\"\n", text, StringComparison.Ordinal);
        Assert.Contains("    ISRC USRC17607839\n", text, StringComparison.Ordinal);
        Assert.Contains("    INDEX 01 00:00:00\n", text, StringComparison.Ordinal);
        Assert.Contains("FILE \"02 - Track Two.flac\" WAVE\n", text, StringComparison.Ordinal);
        Assert.Contains("  TRACK 02 AUDIO\n", text, StringComparison.Ordinal);
        Assert.Contains("    ISRC USRC17607840\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_KnownPregap_SplitsIndexZeroIntoPreviousTrackFileBlock()
    {
        var releaseInfo = CreateReleaseInfo("Artist", "Album");
        var tracks = new (TrackInfo, string)[]
        {
            (new TrackInfo(1, "Track One", "Artist", TimeSpan.FromSeconds(100)), "01.flac"),
            (new TrackInfo(2, "Track Two", "Artist", TimeSpan.FromSeconds(101)), "02.flac"),
        };

        // Track 1 spans frames 0..7499 (7500 frames); track 2's pregap is
        // 150 frames, so its INDEX 00 should land at offset 7500-150=7350
        // frames = 98:00:00 (98*75=7350).
        var toc = new DiscToc(
            [
                new DiscTocTrack(1, 0, 7499, IsAudio: true),
                new DiscTocTrack(2, 7500, 14999, IsAudio: true, PregapFrames: 150),
            ]);

        var text = CueSheetFile.Format(releaseInfo, tracks, toc);

        var file1Index = text.IndexOf("FILE \"01.flac\" WAVE", StringComparison.Ordinal);
        var track2HeaderIndex = text.IndexOf("TRACK 02 AUDIO", StringComparison.Ordinal);
        var index00Index = text.IndexOf("INDEX 00 01:38:00", StringComparison.Ordinal);
        var file2Index = text.IndexOf("FILE \"02.flac\" WAVE", StringComparison.Ordinal);

        Assert.True(file1Index >= 0 && track2HeaderIndex >= 0 && index00Index >= 0 && file2Index >= 0);
        Assert.True(file1Index < track2HeaderIndex, "Track 2's header should be emitted before the second FILE line.");
        Assert.True(track2HeaderIndex < index00Index, "INDEX 00 should follow track 2's header.");
        Assert.True(index00Index < file2Index, "INDEX 00 should still be under the first FILE block.");

        // The second FILE block must not repeat the TRACK/TITLE/PERFORMER
        // header -- only INDEX 01 continues it.
        var textAfterFile2 = text[file2Index..];
        Assert.DoesNotContain("TRACK 02 AUDIO", textAfterFile2, StringComparison.Ordinal);
        Assert.Contains("INDEX 01 00:00:00", textAfterFile2, StringComparison.Ordinal);

        // Only one TRACK 02 header in the whole file.
        Assert.Equal(1, CountOccurrences(text, "TRACK 02 AUDIO"));
    }

    [Fact]
    public void Format_UnscannedPregap_OmitsIndexZero()
    {
        var releaseInfo = CreateReleaseInfo("Artist", "Album");
        var tracks = new (TrackInfo, string)[]
        {
            (new TrackInfo(1, "Track One", "Artist", TimeSpan.FromSeconds(100)), "01.flac"),
            (new TrackInfo(2, "Track Two", "Artist", TimeSpan.FromSeconds(101)), "02.flac"),
        };
        var toc = new DiscToc(
            [
                new DiscTocTrack(1, 0, 7499, IsAudio: true),
                new DiscTocTrack(2, 7500, 14999, IsAudio: true, PregapFrames: null),
            ]);

        var text = CueSheetFile.Format(releaseInfo, tracks, toc);

        Assert.DoesNotContain("INDEX 00", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_Track1OwnPregap_IsNeverRendered()
    {
        // Fast-toc mode is the only mode that ever reports track 1's own
        // pregap, but that pregap is never actually captured in any ripped
        // file (cd-paranoia reads exactly [StartFrame, EndFrame], and there
        // is no "previous track" for track 1's own pregap to be the tail of).
        var releaseInfo = CreateReleaseInfo("Artist", "Album");
        var tracks = new (TrackInfo, string)[]
        {
            (new TrackInfo(1, "Track One", "Artist", TimeSpan.FromSeconds(100)), "01.flac"),
        };
        var toc = new DiscToc([new DiscTocTrack(1, 0, 7499, IsAudio: true, PregapFrames: 150)]);

        var text = CueSheetFile.Format(releaseInfo, tracks, toc);

        Assert.DoesNotContain("INDEX 00", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_DegradedRip_DoesNotBridgePregapAcrossMissingTrack()
    {
        // Track 2 is missing from the matched list (e.g. it exhausted its
        // retries); track 3's pregap must not be attributed to track 1's
        // file even though it's the "previous" entry in the list.
        var releaseInfo = CreateReleaseInfo("Artist", "Album");
        var tracks = new (TrackInfo, string)[]
        {
            (new TrackInfo(1, "Track One", "Artist", TimeSpan.FromSeconds(100)), "01.flac"),
            (new TrackInfo(3, "Track Three", "Artist", TimeSpan.FromSeconds(102)), "03.flac"),
        };
        var toc = new DiscToc(
            [
                new DiscTocTrack(1, 0, 7499, IsAudio: true),
                new DiscTocTrack(2, 7500, 14999, IsAudio: true, PregapFrames: 150),
                new DiscTocTrack(3, 15000, 22499, IsAudio: true, PregapFrames: 150),
            ]);

        var text = CueSheetFile.Format(releaseInfo, tracks, toc);

        Assert.DoesNotContain("INDEX 00", text, StringComparison.Ordinal);
        Assert.Contains("TRACK 03 AUDIO", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_VariousArtists_UsesPerTrackPerformer()
    {
        var releaseInfo = CreateReleaseInfo("Various Artists", "Compilation");
        var tracks = new (TrackInfo, string)[]
        {
            (new TrackInfo(1, "Song A", "Artist A", TimeSpan.FromSeconds(100)), "01.flac"),
            (new TrackInfo(2, "Song B", "Artist B", TimeSpan.FromSeconds(101)), "02.flac"),
        };

        var text = CueSheetFile.Format(releaseInfo, tracks);

        Assert.Contains("PERFORMER \"Various Artists\"\n", text, StringComparison.Ordinal);
        Assert.Contains("    PERFORMER \"Artist A\"\n", text, StringComparison.Ordinal);
        Assert.Contains("    PERFORMER \"Artist B\"\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NoToc_OmitsCatalogIsrcAndPregap()
    {
        var releaseInfo = CreateReleaseInfo("Artist", "Album");
        var tracks = new (TrackInfo, string)[]
        {
            (new TrackInfo(1, "Track One", "Artist", TimeSpan.FromSeconds(100)), "01.flac"),
            (new TrackInfo(2, "Track Two", "Artist", TimeSpan.FromSeconds(101)), "02.flac"),
        };

        var text = CueSheetFile.Format(releaseInfo, tracks);

        Assert.DoesNotContain("CATALOG", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ISRC", text, StringComparison.Ordinal);
        Assert.DoesNotContain("INDEX 00", text, StringComparison.Ordinal);
        Assert.Contains("FILE \"01.flac\" WAVE\n", text, StringComparison.Ordinal);
        Assert.Contains("FILE \"02.flac\" WAVE\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_MultiDisc_EachDiscIsSelfContained()
    {
        var releaseInfo = CreateReleaseInfo("Artist", "Double Album");
        var disc1Tracks = new (TrackInfo, string)[]
        {
            (new TrackInfo(1, "D1 Track One", "Artist", TimeSpan.FromSeconds(100)), "01.flac"),
        };
        var disc2Tracks = new (TrackInfo, string)[]
        {
            (new TrackInfo(1, "D2 Track One", "Artist", TimeSpan.FromSeconds(100)), "01.flac"),
        };

        var disc1Text = CueSheetFile.Format(releaseInfo, disc1Tracks);
        var disc2Text = CueSheetFile.Format(releaseInfo, disc2Tracks);

        Assert.Contains("D1 Track One", disc1Text, StringComparison.Ordinal);
        Assert.DoesNotContain("D2 Track One", disc1Text, StringComparison.Ordinal);
        Assert.Contains("D2 Track One", disc2Text, StringComparison.Ordinal);
        Assert.DoesNotContain("D1 Track One", disc2Text, StringComparison.Ordinal);
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

    private static ReleaseInfo CreateReleaseInfo(string artist, string title) =>
        new(
            MusicBrainzReleaseId: "release-id",
            MusicBrainzUrl: "https://musicbrainz.org/release/release-id",
            Artist: artist,
            Title: title,
            Date: "2000-01-01",
            Country: "US",
            Barcode: null,
            Label: null,
            CatalogNumber: null,
            Media: [new MediumInfo(1, null, [])]);
}
