using Whatinator.Core.Metadata;

namespace Whatinator.Core.Tests;

public class ReleaseInfoFileTests : IDisposable
{
    private readonly string _tempDir;

    public ReleaseInfoFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-releaseinfo-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Save_Then_Load_RoundTripsAllFields()
    {
        List<TrackInfo> tracks =
        [
            new TrackInfo(1, "Why", "Annie Lennox", TimeSpan.FromSeconds(293.73)),
            new TrackInfo(2, "Walking on Broken Glass", "Annie Lennox", TimeSpan.FromSeconds(253.27)),
        ];
        List<MediumInfo> media = [new MediumInfo(Position: 1, Subtitle: "Venus Orbiting", Tracks: tracks)];
        var original = new ReleaseInfo(
            MusicBrainzReleaseId: "13856621-72e0-4a14-b519-69513aae579f",
            MusicBrainzUrl: "https://musicbrainz.org/release/13856621-72e0-4a14-b519-69513aae579f",
            Artist: "Annie Lennox",
            Title: "Diva",
            Date: "1992-05-12",
            Country: "US",
            Barcode: "078221870429",
            Label: "Arista",
            CatalogNumber: "07822-18704-2",
            Media: media);

        var path = Path.Combine(_tempDir, "releaseinfo.json");
        ReleaseInfoFile.Save(original, path);
        var loaded = ReleaseInfoFile.Load(path);

        Assert.Equal(original.MusicBrainzReleaseId, loaded.MusicBrainzReleaseId);
        Assert.Equal(original.MusicBrainzUrl, loaded.MusicBrainzUrl);
        Assert.Equal(original.Artist, loaded.Artist);
        Assert.Equal(original.Title, loaded.Title);
        Assert.Equal(original.Date, loaded.Date);
        Assert.Equal(original.Country, loaded.Country);
        Assert.Equal(original.Barcode, loaded.Barcode);
        Assert.Equal(original.Label, loaded.Label);
        Assert.Equal(original.CatalogNumber, loaded.CatalogNumber);
        var loadedMedium = Assert.Single(loaded.Media);
        var originalMedium = Assert.Single(original.Media);
        Assert.Equal(originalMedium.Position, loadedMedium.Position);
        Assert.Equal(originalMedium.Subtitle, loadedMedium.Subtitle);
        Assert.Equal(originalMedium.Tracks, loadedMedium.Tracks);
    }

    [Fact]
    public void Save_WritesIndentedJson()
    {
        var releaseInfo = new ReleaseInfo(
            "id", "url", "Artist", "Title", null, null, null, null, null, Media: []);
        var path = Path.Combine(_tempDir, "releaseinfo.json");

        ReleaseInfoFile.Save(releaseInfo, path);

        var content = File.ReadAllText(path);
        Assert.Contains('\n', content);
        Assert.Contains("\"musicBrainzReleaseId\"", content, StringComparison.Ordinal);
    }
}
