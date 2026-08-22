using Whatinator.Core.Discogs;
using Whatinator.Core.Metadata;

namespace Whatinator.Core.Tests;

public class IdTextFileTests
{
    private static readonly DiscogsInfo SampleDiscogs = new(
        Id: "139878",
        Title: "Annie Lennox - Diva",
        Country: "US",
        Format: "CD, Album",
        Genre: "Electronic",
        Style: "Synth-pop",
        Label: "Arista",
        CatalogNumber: "07822-18704-2",
        Url: "https://www.discogs.com/release/139878-Annie-Lennox-Diva");

    [Fact]
    public void Format_IncludesHeaderFieldsFromDiscogsAndMusicBrainz()
    {
        var releaseInfo = CreateReleaseInfo(discogs: SampleDiscogs, date: "1992-05-12");

        var text = IdTextFile.Format(releaseInfo);

        Assert.Contains("artist: Annie Lennox\n", text, StringComparison.Ordinal);
        Assert.Contains("title: Diva\n", text, StringComparison.Ordinal);
        Assert.Contains("medium: cd\n", text, StringComparison.Ordinal);
        Assert.Contains("release: Arista - 07822-18704-2\n", text, StringComparison.Ordinal);
        Assert.Contains("series: -\n", text, StringComparison.Ordinal);
        Assert.Contains("format: CD, Album\n", text, StringComparison.Ordinal);
        Assert.Contains("country: US\n", text, StringComparison.Ordinal);
        Assert.Contains("released: May 12, 1992\n", text, StringComparison.Ordinal);
        Assert.Contains("genre: Electronic\n", text, StringComparison.Ordinal);
        Assert.Contains("style: Synth-pop\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesDashForMissingFields()
    {
        var releaseInfo = CreateReleaseInfo(discogs: null, date: null, label: null, catalogNumber: null, country: null);

        var text = IdTextFile.Format(releaseInfo);

        Assert.Contains("release: -\n", text, StringComparison.Ordinal);
        Assert.Contains("format: -\n", text, StringComparison.Ordinal);
        Assert.Contains("country: -\n", text, StringComparison.Ordinal);
        Assert.Contains("released: -\n", text, StringComparison.Ordinal);
        Assert.Contains("genre: -\n", text, StringComparison.Ordinal);
        Assert.Contains("style: -\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_OmitsDiscogsLink_WhenNoDiscogsMatch()
    {
        var releaseInfo = CreateReleaseInfo(discogs: null);

        var text = IdTextFile.Format(releaseInfo);

        Assert.DoesNotContain("discogs.com", text, StringComparison.Ordinal);
        Assert.Contains(releaseInfo.MusicBrainzUrl, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesDiscogsLinkBeforeMusicBrainzLink_WhenDiscogsMatched()
    {
        var releaseInfo = CreateReleaseInfo(discogs: SampleDiscogs);

        var text = IdTextFile.Format(releaseInfo);

        var discogsIndex = text.IndexOf(SampleDiscogs.Url, StringComparison.Ordinal);
        var musicBrainzIndex = text.IndexOf(releaseInfo.MusicBrainzUrl, StringComparison.Ordinal);
        Assert.True(discogsIndex >= 0 && discogsIndex < musicBrainzIndex);
    }

    [Theory]
    [InlineData("1992-05-12", "May 12, 1992")]
    [InlineData("1992-05", "May 1992")]
    [InlineData("1992", "1992")]
    [InlineData(null, "-")]
    public void Format_FormatsReleaseDate(string? date, string expected)
    {
        var releaseInfo = CreateReleaseInfo(date: date);

        var text = IdTextFile.Format(releaseInfo);

        Assert.Contains($"released: {expected}\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NormalizesNonStandardDashes_ToAsciiHyphen()
    {
        var releaseInfo = CreateReleaseInfo(label: "Atlantic – East West", catalogNumber: "83230–2");

        var text = IdTextFile.Format(releaseInfo);

        Assert.Contains("release: Atlantic - East West - 83230-2\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain('–', text);
        Assert.DoesNotContain('—', text);
    }

    [Fact]
    public void Format_SingleDisc_HasNoDiscHeaderLine()
    {
        var releaseInfo = CreateReleaseInfo();

        var text = IdTextFile.Format(releaseInfo);

        Assert.DoesNotContain("Disc 1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_MultiDisc_IncludesSubtitleHeadersAndPerDiscTrackNumbers()
    {
        List<MediumInfo> media =
        [
            new MediumInfo(1, "Venus Orbiting", [new TrackInfo(1, "Bliss", "Tori Amos", TimeSpan.FromSeconds(222))]),
            new MediumInfo(2, "Venus Live, Still Orbiting", [new TrackInfo(1, "Precious Things", "Tori Amos", TimeSpan.FromSeconds(457))]),
        ];
        var releaseInfo = CreateReleaseInfo(media: media);

        var text = IdTextFile.Format(releaseInfo);

        Assert.Contains("Disc 1 - Venus Orbiting\n", text, StringComparison.Ordinal);
        Assert.Contains("Disc 2 - Venus Live, Still Orbiting\n", text, StringComparison.Ordinal);
        Assert.Contains("01 Bliss", text, StringComparison.Ordinal);
        Assert.Contains("01 Precious Things", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_AlignsTrackDurationColumn()
    {
        List<TrackInfo> tracks =
        [
            new TrackInfo(1, "Why", "Annie Lennox", TimeSpan.FromSeconds(293)),
            new TrackInfo(2, "Walking On Broken Glass", "Annie Lennox", TimeSpan.FromSeconds(252)),
        ];
        var releaseInfo = CreateReleaseInfo(media: [new MediumInfo(1, null, tracks)]);

        var text = IdTextFile.Format(releaseInfo);

        Assert.Contains("01 Why                      4:53\n", text, StringComparison.Ordinal);
        Assert.Contains("02 Walking On Broken Glass  4:12\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_CapsAlignmentAt80Chars_WithoutDraggingOtherLinesOut()
    {
        var longTitle = new string('x', 100);
        List<TrackInfo> tracks =
        [
            new TrackInfo(1, longTitle, "Artist", TimeSpan.FromSeconds(60)),
            new TrackInfo(2, "Short", "Artist", TimeSpan.FromSeconds(60)),
        ];
        var releaseInfo = CreateReleaseInfo(media: [new MediumInfo(1, null, tracks)]);

        var text = IdTextFile.Format(releaseInfo);
        var lines = text.Split('\n');
        var longLine = Array.Find(lines, l => l.StartsWith("01 ", StringComparison.Ordinal));
        var shortLine = Array.Find(lines, l => l.StartsWith("02 ", StringComparison.Ordinal));

        Assert.NotNull(longLine);
        Assert.NotNull(shortLine);
        Assert.True(longLine!.Length > 80, "an outlier-length title is expected to overflow 80 chars");
        Assert.True(shortLine!.Length <= 80, "other lines shouldn't be dragged out by one outlier title");
    }

    private static ReleaseInfo CreateReleaseInfo(
        DiscogsInfo? discogs = null,
        string? date = "1992-05-12",
        string? label = "Arista",
        string? catalogNumber = "07822-18704-2",
        string? country = "US",
        IReadOnlyList<MediumInfo>? media = null) => new(
        MusicBrainzReleaseId: "13856621-72e0-4a14-b519-69513aae579f",
        MusicBrainzUrl: "https://musicbrainz.org/release/13856621-72e0-4a14-b519-69513aae579f",
        Artist: "Annie Lennox",
        Title: "Diva",
        Date: date,
        Country: country,
        Barcode: "078221870429",
        Label: label,
        CatalogNumber: catalogNumber,
        Media: media ?? [new MediumInfo(1, null, [new TrackInfo(1, "Why", "Annie Lennox", TimeSpan.FromSeconds(293))])],
        Discogs: discogs);
}
