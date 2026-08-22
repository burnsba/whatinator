using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;

namespace Whatinator.Core.Tests;

public class TrackFileNamingTests
{
    [Fact]
    public void UsesPerTrackArtist_False_WhenEveryTrackMatchesReleaseArtist()
    {
        var releaseInfo = CreateReleaseInfo(
            releaseArtist: "Tori Amos",
            trackArtists: ["Tori Amos", "Tori Amos", "Tori Amos"]);

        Assert.False(TrackFileNaming.UsesPerTrackArtist(releaseInfo));
    }

    [Fact]
    public void UsesPerTrackArtist_True_WhenAnyTrackArtistDiffers()
    {
        var releaseInfo = CreateReleaseInfo(
            releaseArtist: "Various Artists",
            trackArtists: ["Tori Amos", "Björk"]);

        Assert.True(TrackFileNaming.UsesPerTrackArtist(releaseInfo));
    }

    [Fact]
    public void UsesPerTrackArtist_ChecksEveryDisc_OnAMultiDiscRelease()
    {
        List<MediumInfo> media =
        [
            new MediumInfo(1, null, [new TrackInfo(1, "Bliss", "Tori Amos", TimeSpan.FromSeconds(222))]),
            new MediumInfo(2, null, [new TrackInfo(1, "Live Track", "Tori Amos (live)", TimeSpan.FromSeconds(200))]),
        ];
        var releaseInfo = new ReleaseInfo(
            MusicBrainzReleaseId: "id",
            MusicBrainzUrl: "https://musicbrainz.org/release/id",
            Artist: "Tori Amos",
            Title: "To Venus and Back",
            Date: "1999-09-21",
            Country: "US",
            Barcode: null,
            Label: null,
            CatalogNumber: null,
            Media: media);

        Assert.True(TrackFileNaming.UsesPerTrackArtist(releaseInfo));
    }

    [Fact]
    public void BuildBaseFileName_OmitsArtist_WhenSingleArtistRelease()
    {
        var releaseInfo = CreateReleaseInfo("Tori Amos", ["Tori Amos"]);
        var track = new TrackInfo(1, "Bliss", "Tori Amos", TimeSpan.FromSeconds(222));

        Assert.Equal("01 - Bliss", TrackFileNaming.BuildBaseFileName(releaseInfo, track));
    }

    [Fact]
    public void BuildBaseFileName_IncludesArtist_WhenVariousArtistsRelease()
    {
        var releaseInfo = CreateReleaseInfo("Various Artists", ["Tori Amos", "Björk"]);
        var track = new TrackInfo(2, "Army of Me", "Björk", TimeSpan.FromSeconds(240));

        Assert.Equal("02 - Björk - Army of Me", TrackFileNaming.BuildBaseFileName(releaseInfo, track));
    }

    [Fact]
    public void BuildBaseFileName_ZeroPadsTrackNumber()
    {
        var releaseInfo = CreateReleaseInfo("Artist", ["Artist"]);
        var track = new TrackInfo(3, "Title", "Artist", TimeSpan.FromSeconds(100));

        Assert.StartsWith("03 - ", TrackFileNaming.BuildBaseFileName(releaseInfo, track));
    }

    [Fact]
    public void BuildBaseFileName_SanitizesForbiddenCharacters()
    {
        var releaseInfo = CreateReleaseInfo("Artist", ["Artist"]);
        var track = new TrackInfo(1, "Question?", "Artist", TimeSpan.FromSeconds(100));

        Assert.Equal("01 - Question_", TrackFileNaming.BuildBaseFileName(releaseInfo, track));
    }

    private static ReleaseInfo CreateReleaseInfo(string releaseArtist, IReadOnlyList<string> trackArtists) => new(
        MusicBrainzReleaseId: "id",
        MusicBrainzUrl: "https://musicbrainz.org/release/id",
        Artist: releaseArtist,
        Title: "Some Album",
        Date: "1999-01-01",
        Country: "US",
        Barcode: null,
        Label: null,
        CatalogNumber: null,
        Media: [new MediumInfo(1, null, trackArtists.Select((artist, i) => new TrackInfo(i + 1, $"Track {i + 1}", artist, TimeSpan.FromSeconds(200))).ToList())]);
}
