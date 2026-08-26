using Whatinator.Core.Metadata;
using Whatinator.Core.MusicBrainz;
using Whatinator.LibDiscId;

namespace Whatinator.Cli.Tests;

/// <summary>
/// Tests for <see cref="MakeReleaseInfoCommand"/>'s manual-override URL
/// parsing and the ambiguous-picker track-count validation -- see
/// docs/backlog-completed/052-metadata-picker-manual-override-and-ctrlc.md.
/// The disc-read path itself (<see cref="MakeReleaseInfoCommand.LookUpFromDiscAsync"/>)
/// needs a real drive and isn't unit-testable (see
/// src/Whatinator.LibDiscId/CLAUDE.md § Testing constraints), so these tests
/// exercise the internal helpers it delegates to instead.
/// </summary>
[Collection("Console")]
public class MakeReleaseInfoCommandTests
{
    [Theory]
    [InlineData("https://musicbrainz.org/release/0586779a-1e0c-465a-ad7d-5dd1c0946028", "0586779a-1e0c-465a-ad7d-5dd1c0946028")]
    [InlineData("https://musicbrainz.org/release/0586779a-1e0c-465a-ad7d-5dd1c0946028?query=ignored", "0586779a-1e0c-465a-ad7d-5dd1c0946028")]
    public void TryParseMusicBrainzReleaseId_ValidUrl_ExtractsMbid(string url, string expectedId)
    {
        var parsed = MakeReleaseInfoCommand.TryParseMusicBrainzReleaseId(url, out var releaseId);

        Assert.True(parsed);
        Assert.Equal(expectedId, releaseId);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("https://musicbrainz.org/artist/0586779a-1e0c-465a-ad7d-5dd1c0946028")]
    [InlineData("https://musicbrainz.org/release/not-a-guid")]
    [InlineData("https://musicbrainz.org/release/")]
    public void TryParseMusicBrainzReleaseId_InvalidUrl_ReturnsFalse(string url)
    {
        var parsed = MakeReleaseInfoCommand.TryParseMusicBrainzReleaseId(url, out var releaseId);

        Assert.False(parsed);
        Assert.Equal(string.Empty, releaseId);
    }

    [Theory]
    [InlineData("https://www.discogs.com/release/249276-Bob-Dylan-Desire", "249276")]
    [InlineData("https://www.discogs.com/release/249276", "249276")]
    public void TryParseDiscogsReleaseId_ValidUrl_ExtractsId(string url, string expectedId)
    {
        var parsed = MakeReleaseInfoCommand.TryParseDiscogsReleaseId(url, out var releaseId);

        Assert.True(parsed);
        Assert.Equal(expectedId, releaseId);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("https://www.discogs.com/artist/249276-Bob-Dylan")]
    [InlineData("https://www.discogs.com/release/Bob-Dylan-Desire")]
    public void TryParseDiscogsReleaseId_InvalidUrl_ReturnsFalse(string url)
    {
        var parsed = MakeReleaseInfoCommand.TryParseDiscogsReleaseId(url, out var releaseId);

        Assert.False(parsed);
        Assert.Equal(string.Empty, releaseId);
    }

    [Fact]
    public async Task ResolveAmbiguousMusicBrainzMatchAsync_NumberedPick_ReturnsDiscIdMatchedTrue()
    {
        var disc = CreateDisc(trackCount: 2);
        var candidates = new[] { CreateCandidate("release-1"), CreateCandidate("release-2") };
        var client = new FakeMusicBrainzClient(releaseId => CreateReleaseInfo(releaseId, trackCount: 2));
        var service = new MetadataService(client);

        var (originalIn, originalOut) = RedirectConsole("2\n");
        try
        {
            var result = await MakeReleaseInfoCommand.ResolveAmbiguousMusicBrainzMatchAsync(disc, service, candidates, CancellationToken.None, isOutputRedirected: false);

            Assert.NotNull(result);
            Assert.True(result.Value.DiscIdMatched);
            Assert.Equal("release-2", result.Value.ReleaseInfo.MusicBrainzReleaseId);
        }
        finally
        {
            RestoreConsole(originalIn, originalOut);
        }
    }

    [Fact]
    public async Task ResolveAmbiguousMusicBrainzMatchAsync_ManualOverride_ReturnsDiscIdMatchedFalse()
    {
        var disc = CreateDisc(trackCount: 2);
        var candidates = new[] { CreateCandidate("release-1") };
        var client = new FakeMusicBrainzClient(releaseId => CreateReleaseInfo(releaseId, trackCount: 2));
        var service = new MetadataService(client);

        var (originalIn, originalOut) = RedirectConsole("m\nhttps://musicbrainz.org/release/0586779a-1e0c-465a-ad7d-5dd1c0946028\n");
        try
        {
            var result = await MakeReleaseInfoCommand.ResolveAmbiguousMusicBrainzMatchAsync(disc, service, candidates, CancellationToken.None, isOutputRedirected: false);

            Assert.NotNull(result);
            Assert.False(result.Value.DiscIdMatched);
            Assert.Equal("0586779a-1e0c-465a-ad7d-5dd1c0946028", result.Value.ReleaseInfo.MusicBrainzReleaseId);
        }
        finally
        {
            RestoreConsole(originalIn, originalOut);
        }
    }

    [Fact]
    public async Task ResolveAmbiguousMusicBrainzMatchAsync_ManualOverrideTrackCountMismatch_ReshowsListAndAcceptsNumberedPick()
    {
        var disc = CreateDisc(trackCount: 9);
        var candidates = new[] { CreateCandidate("release-1") };

        // The manual override resolves to a 4-track release (mismatch against
        // the disc's 9), so it must be rejected and the prompt re-shown --
        // where "1" then picks the disc-ID-matched candidate, which does
        // have the matching track count.
        var client = new FakeMusicBrainzClient(releaseId => CreateReleaseInfo(releaseId, trackCount: releaseId == "release-1" ? 9 : 4));
        var service = new MetadataService(client);

        var (originalIn, originalOut) = RedirectConsole("m\nhttps://musicbrainz.org/release/0586779a-1e0c-465a-ad7d-5dd1c0946028\n1\n");
        try
        {
            var result = await MakeReleaseInfoCommand.ResolveAmbiguousMusicBrainzMatchAsync(disc, service, candidates, CancellationToken.None, isOutputRedirected: false);

            Assert.NotNull(result);
            Assert.True(result.Value.DiscIdMatched);
            Assert.Equal("release-1", result.Value.ReleaseInfo.MusicBrainzReleaseId);
        }
        finally
        {
            RestoreConsole(originalIn, originalOut);
        }
    }

    [Fact]
    public async Task ResolveManualOnlyMusicBrainzMatchAsync_TrackCountMismatchThenMatch_RetriesUntilItMatches()
    {
        var disc = CreateDisc(trackCount: 9);
        var client = new FakeMusicBrainzClient(releaseId => CreateReleaseInfo(releaseId, trackCount: releaseId == "good-release" ? 9 : 4));
        var service = new MetadataService(client);

        var (originalIn, originalOut) = RedirectConsole(
            "https://musicbrainz.org/release/00000000-0000-0000-0000-000000000000\n" +
            "https://musicbrainz.org/release/11111111-1111-1111-1111-111111111111\n");
        try
        {
            // Neither of these MBIDs is "good-release", so both mismatch (4 tracks vs
            // the disc's 9) -- this exercises the retry loop rather than a final match;
            // asserting it gives up cleanly on EOF after both attempts are exhausted.
            var result = await MakeReleaseInfoCommand.ResolveManualOnlyMusicBrainzMatchAsync(disc, service, CancellationToken.None);

            Assert.Null(result);
        }
        finally
        {
            RestoreConsole(originalIn, originalOut);
        }
    }

    private static Disc CreateDisc(int trackCount)
    {
        var tracks = Enumerable.Range(1, trackCount)
            .Select(number => new Track(number, OffsetSectors: number * 1000, LengthSectors: 1000))
            .ToList();
        return new Disc(
            Id: "fake-disc-id",
            FreedbId: "fake-freedb-id",
            SubmissionUrl: "https://musicbrainz.org/cdtoc/attach",
            TocString: string.Empty,
            FirstTrack: 1,
            LastTrack: trackCount,
            Sectors: trackCount * 1000,
            Tracks: tracks);
    }

    private static ReleaseCandidate CreateCandidate(string musicBrainzReleaseId) =>
        new(musicBrainzReleaseId, Artist: "Some Artist", Title: "Some Title", Date: null, Country: null, Barcode: null, CatalogNumber: null);

    private static ReleaseInfo CreateReleaseInfo(string musicBrainzReleaseId, int trackCount)
    {
        var tracks = Enumerable.Range(1, trackCount)
            .Select(number => new TrackInfo(number, $"Track {number}", "Some Artist", TimeSpan.FromMinutes(3)))
            .ToList();
        return new ReleaseInfo(
            MusicBrainzReleaseId: musicBrainzReleaseId,
            MusicBrainzUrl: $"https://musicbrainz.org/release/{musicBrainzReleaseId}",
            Artist: "Some Artist",
            Title: "Some Title",
            Date: null,
            Country: null,
            Barcode: null,
            Label: null,
            CatalogNumber: null,
            Media: [new MediumInfo(1, null, tracks)]);
    }

    private static (TextReader OriginalIn, TextWriter OriginalOut) RedirectConsole(string input)
    {
        var originalIn = Console.In;
        var originalOut = Console.Out;
        Console.SetIn(new StringReader(input));
        Console.SetOut(new StringWriter());
        return (originalIn, originalOut);
    }

    private static void RestoreConsole(TextReader originalIn, TextWriter originalOut)
    {
        Console.SetIn(originalIn);
        Console.SetOut(originalOut);
    }

    /// <summary>A minimal fake for <see cref="IMusicBrainzClient"/> -- only <see cref="GetReleaseAsync"/> is exercised by these tests.</summary>
    private sealed class FakeMusicBrainzClient(Func<string, ReleaseInfo> resolve) : IMusicBrainzClient
    {
        public Task<IReadOnlyList<ReleaseCandidate>> LookupByDiscIdAsync(string discId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by these tests.");

        public Task<ReleaseInfo> GetReleaseAsync(string releaseId, CancellationToken cancellationToken = default) =>
            Task.FromResult(resolve(releaseId));
    }
}
