using Whatinator.Core.Metadata;

namespace Whatinator.Core.Tests;

public class MetadataServiceTests
{
    private static readonly ReleaseInfo SampleRelease = new(
        MusicBrainzReleaseId: "release-1",
        MusicBrainzUrl: "https://musicbrainz.org/release/release-1",
        Artist: "Annie Lennox",
        Title: "Diva",
        Date: "1992-05-12",
        Country: "US",
        Barcode: "078221870429",
        Label: "Arista",
        CatalogNumber: "07822-18704-2",
        Media: []);

    [Fact]
    public async Task LookupByDiscIdAsync_ReturnsNotFound_WhenNoCandidates()
    {
        var client = new FakeMusicBrainzClient(candidates: [], releases: []);
        var service = new MetadataService(client);

        var result = await service.LookupByDiscIdAsync("some-disc-id");

        Assert.Equal(MetadataLookupStatus.NotFound, result.Status);
        Assert.Null(result.ReleaseInfo);
        Assert.Null(result.Candidates);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_ReturnsFoundWithFullRelease_WhenExactlyOneCandidate()
    {
        var candidate = new ReleaseCandidate("release-1", "Annie Lennox", "Diva", "1992-05-12", "US", "078221870429", "07822-18704-2");
        var client = new FakeMusicBrainzClient(candidates: [candidate], releases: [SampleRelease]);
        var service = new MetadataService(client);

        var result = await service.LookupByDiscIdAsync("some-disc-id");

        Assert.Equal(MetadataLookupStatus.Found, result.Status);
        Assert.Same(SampleRelease, result.ReleaseInfo);
        Assert.Equal(1, client.GetReleaseCallCount);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_ReturnsAmbiguous_WithoutFetchingFullRelease_WhenMultipleCandidates()
    {
        var candidates = new[]
        {
            new ReleaseCandidate("release-1", "Annie Lennox", "Diva (US pressing)", "1992-05-12", "US", "078221870429", "07822-18704-2"),
            new ReleaseCandidate("release-2", "Annie Lennox", "Diva (UK pressing)", "1992-04-06", "GB", null, null),
        };
        var client = new FakeMusicBrainzClient(candidates, releases: []);
        var service = new MetadataService(client);

        var result = await service.LookupByDiscIdAsync("some-disc-id");

        Assert.Equal(MetadataLookupStatus.Ambiguous, result.Status);
        Assert.Equal(candidates, result.Candidates);
        Assert.Equal(0, client.GetReleaseCallCount);
    }

    [Fact]
    public async Task ResolveAsync_DelegatesToClient()
    {
        var client = new FakeMusicBrainzClient(candidates: [], releases: [SampleRelease]);
        var service = new MetadataService(client);

        var result = await service.ResolveAsync("release-1");

        Assert.Same(SampleRelease, result);
    }

    [Fact]
    public void Constructor_ThrowsOnNullClient()
    {
        Assert.Throws<ArgumentNullException>(() => new MetadataService(null!));
    }
}
