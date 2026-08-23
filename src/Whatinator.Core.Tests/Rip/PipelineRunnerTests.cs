using Whatinator.Core.AccurateRip;
using Whatinator.Core.CoverArt;
using Whatinator.Core.Metadata;
using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

/// <summary>
/// <see cref="PipelineRunner.RunDiscAsync"/> shells out to real
/// <c>cdrdao</c>/<c>cd-paranoia</c>/<c>flac</c> binaries against real
/// hardware for anything past disc-number validation, same as
/// <see cref="WhatinatorRipRunner.RipAsync"/> itself -- not something a unit
/// test can exercise end-to-end. Only the validation that happens before any
/// of that is invoked is covered here.
/// </summary>
public class PipelineRunnerTests
{
    [Fact]
    public async Task RunDiscAsync_ThrowsBeforeRipping_WhenDiscNumberMissingForMultiDisc()
    {
        var releaseInfo = CreateMultiDiscRelease();
        var runner = new PipelineRunner(new FakeCoverArtClient(), new FakeAccurateRipClient());
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunDiscAsync(
            new PipelineDiscOptions(releaseInfo, DiscNumber: null, Device: "/dev/sr1", DestinationParentDirectory: ".", SkipFlacPackaging: false, CreateMp3: false),
            stdout,
            stderr));
    }

    [Fact]
    public async Task RunDiscAsync_ThrowsBeforeRipping_WhenDiscNumberOutOfRange()
    {
        var releaseInfo = CreateMultiDiscRelease();
        var runner = new PipelineRunner(new FakeCoverArtClient(), new FakeAccurateRipClient());
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RunDiscAsync(
            new PipelineDiscOptions(releaseInfo, DiscNumber: 5, Device: "/dev/sr1", DestinationParentDirectory: ".", SkipFlacPackaging: false, CreateMp3: false),
            stdout,
            stderr));
    }

    private static ReleaseInfo CreateMultiDiscRelease()
    {
        List<TrackInfo> disc1Tracks = [new TrackInfo(1, "D1 Track One", "Artist", TimeSpan.FromSeconds(100))];
        List<TrackInfo> disc2Tracks = [new TrackInfo(1, "D2 Track One", "Artist", TimeSpan.FromSeconds(100))];

        return new ReleaseInfo(
            MusicBrainzReleaseId: "release-id",
            MusicBrainzUrl: "https://musicbrainz.org/release/release-id",
            Artist: "Artist",
            Title: "Album",
            Date: "2000-01-01",
            Country: "US",
            Barcode: null,
            Label: null,
            CatalogNumber: null,
            Media: [new MediumInfo(1, null, disc1Tracks), new MediumInfo(2, null, disc2Tracks)]);
    }

    private sealed class FakeCoverArtClient : ICoverArtClient
    {
        public Task<CoverArtResult?> TryDownloadFrontCoverAsync(string musicBrainzReleaseId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CoverArtResult?>(null);
    }

    private sealed class FakeAccurateRipClient : IAccurateRipClient
    {
        public Task<AccurateRipMatchResult> MatchAsync(
            DiscToc toc,
            IReadOnlyList<(uint V1, uint V2)> computedChecksums,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AccurateRipMatchResult { Found = false, Tracks = [] });
    }
}
