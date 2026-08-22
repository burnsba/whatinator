using Whatinator.Core.Metadata;
using Whatinator.Core.Rip;

namespace Whatinator.Core.Tests;

public class TrackFileMatcherTests
{
    private static readonly List<TrackInfo> ThreeTracks =
    [
        new TrackInfo(1, "Track One", "Artist", TimeSpan.FromSeconds(100)),
        new TrackInfo(2, "Track Two", "Artist", TimeSpan.FromSeconds(101)),
        new TrackInfo(3, "Track Three", "Artist", TimeSpan.FromSeconds(102)),
    ];

    [Fact]
    public void Match_PairsEveryTrack_WhenAllFilesPresent()
    {
        string[] files =
        [
            "/disc/01. Artist - Track One.flac",
            "/disc/02. Artist - Track Two.flac",
            "/disc/03. Artist - Track Three.flac",
        ];

        var matches = TrackFileMatcher.Match(files, ThreeTracks);

        Assert.Equal(3, matches.Count);
        Assert.Equal(ThreeTracks[0], matches[0].Track);
        Assert.Equal(files[0], matches[0].FilePath);
        Assert.Equal(ThreeTracks[2], matches[2].Track);
    }

    [Fact]
    public void Match_SkipsMissingTrack_WhenAFileIsAbsent()
    {
        string[] files =
        [
            "/disc/01. Artist - Track One.flac",
            "/disc/03. Artist - Track Three.flac",
        ];

        var matches = TrackFileMatcher.Match(files, ThreeTracks);

        Assert.Equal(2, matches.Count);
        Assert.Equal(1, matches[0].Track.Number);
        Assert.Equal(3, matches[1].Track.Number);
    }

    [Fact]
    public void Match_ReturnsInTrackNumberOrder_RegardlessOfFileOrder()
    {
        string[] files =
        [
            "/disc/03. Artist - Track Three.flac",
            "/disc/01. Artist - Track One.flac",
            "/disc/02. Artist - Track Two.flac",
        ];

        var matches = TrackFileMatcher.Match(files, ThreeTracks);

        Assert.Equal([1, 2, 3], matches.Select(m => m.Track.Number));
    }

    [Fact]
    public void Match_IgnoresFilesWithNoLeadingTrackNumber()
    {
        string[] files = ["/disc/cover.flac", "/disc/01. Artist - Track One.flac"];

        var matches = TrackFileMatcher.Match(files, ThreeTracks);

        Assert.Single(matches);
        Assert.Equal(1, matches[0].Track.Number);
    }

    [Fact]
    public void Match_ReturnsEmpty_WhenNoFilesMatch()
    {
        string[] files = ["/disc/readme.txt"];

        var matches = TrackFileMatcher.Match(files, ThreeTracks);

        Assert.Empty(matches);
    }

    [Fact]
    public void Match_PairsEveryTrack_WithDashSeparatorAndNoArtist()
    {
        // TrackFileNaming.BuildBaseFileName's single-artist form (no per-track artist).
        string[] files =
        [
            "/disc/01 - Track One.flac",
            "/disc/02 - Track Two.flac",
            "/disc/03 - Track Three.flac",
        ];

        var matches = TrackFileMatcher.Match(files, ThreeTracks);

        Assert.Equal(3, matches.Count);
        Assert.Equal(1, matches[0].Track.Number);
        Assert.Equal(files[0], matches[0].FilePath);
    }

    [Fact]
    public void Match_PairsEveryTrack_WithDashSeparatorAndArtist()
    {
        // TrackFileNaming.BuildBaseFileName's various-artists form (per-track artist included).
        string[] files =
        [
            "/disc/01 - Artist - Track One.flac",
            "/disc/02 - Artist - Track Two.flac",
            "/disc/03 - Artist - Track Three.flac",
        ];

        var matches = TrackFileMatcher.Match(files, ThreeTracks);

        Assert.Equal(3, matches.Count);
        Assert.Equal(1, matches[0].Track.Number);
        Assert.Equal(files[0], matches[0].FilePath);
    }
}
