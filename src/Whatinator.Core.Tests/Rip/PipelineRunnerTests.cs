using Whatinator.Core.AccurateRip;
using Whatinator.Core.CoverArt;
using Whatinator.Core.Drive;
using Whatinator.Core.Flac;
using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;
using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

/// <summary>
/// <see cref="PipelineRunner.RunDiscAsync"/> shells out to real
/// <c>cdrdao</c>/<c>cd-paranoia</c>/<c>flac</c> binaries against real
/// hardware for anything past disc-number validation, same as
/// <see cref="WhatinatorRipRunner.RipAsync"/> itself -- not something a unit
/// test can exercise end-to-end. Only the validation that happens before any
/// of that is invoked is covered here, plus behavioral checks (below) that
/// <see cref="ReleaseFolderNaming.ResolveDiscDirectory"/> -- the same
/// resolver <see cref="PipelineRunner.RunDiscAsync"/> uses to predict the rip
/// log's <c>Filename</c> lines -- agrees with what <see cref="FlacPackager"/>
/// actually produces. <c>--no-flac</c> (<see cref="PipelineDiscOptions.SkipFlacPackaging"/>)
/// isn't covered here for the same hardware reason: the branch that matters
/// (leaving raw output in place, skipping <see cref="FlacPackager"/>) only
/// runs after a real rip completes.
/// </summary>
public class PipelineRunnerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-pipelinerunner-tests-" + Guid.NewGuid());

    /// <summary>Removes the temp directory this test class's packaging tests wrote into.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

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

    [Fact]
    public async Task ResolveDiscDirectory_MultiDisc_AgreesWithWhatFlacPackagerActuallyProduces()
    {
        var releaseInfo = CreateMultiDiscRelease();
        var (destinationParentDirectory, sourceDir) = CreatePackagingDirs();
        CreateFakeFlacFile(sourceDir, "01 - D2 Track One.flac");

        var predicted = ReleaseFolderNaming.ResolveDiscDirectory(releaseInfo, destinationParentDirectory, "flac", discNumber: 2).DiscDirectory;

        var packager = new FlacPackager(new FakeCoverArtClient());
        var actual = await packager.PackageAsync(new FlacPackageOptions(releaseInfo, sourceDir, destinationParentDirectory, DiscNumber: 2));

        Assert.Equal(predicted, actual.DiscDirectory);
    }

    [Fact]
    public async Task ResolveDiscDirectory_SingleDisc_AgreesWithWhatFlacPackagerActuallyProduces()
    {
        var releaseInfo = CreateSingleDiscRelease();
        var (destinationParentDirectory, sourceDir) = CreatePackagingDirs();
        CreateFakeFlacFile(sourceDir, "01 - Track One.flac");

        var predicted = ReleaseFolderNaming.ResolveDiscDirectory(releaseInfo, destinationParentDirectory, "flac", discNumber: 1).DiscDirectory;

        var packager = new FlacPackager(new FakeCoverArtClient());
        var actual = await packager.PackageAsync(new FlacPackageOptions(releaseInfo, sourceDir, destinationParentDirectory));

        Assert.Equal(predicted, actual.DiscDirectory);
    }

    [Fact]
    public async Task EacLogFilenameLines_ResolveToRealFiles_AfterPackagingCompletes()
    {
        // Mirrors PipelineRunner.RunDiscAsync's own sequence: predict the
        // disc directory via the same resolver, write a log referencing it,
        // then actually package -- and confirm the log's Filename line now
        // points at a real file.
        var releaseInfo = CreateSingleDiscRelease();
        var (destinationParentDirectory, sourceDir) = CreatePackagingDirs();
        const string flacFileName = "01 - Track One.flac";
        CreateFakeFlacFile(sourceDir, flacFileName);

        var predictedDiscDirectory = ReleaseFolderNaming.ResolveDiscDirectory(releaseInfo, destinationParentDirectory, "flac", discNumber: 1).DiscDirectory;
        var toc = new DiscToc([new DiscTocTrack(1, 0, 100, IsAudio: true)]);
        var ripResult = new WhatinatorRipResult(
            [new WhatinatorTrackRipResult(1, Degraded: false, FlacFilePath: Path.Combine(sourceDir, flacFileName), WavFilePath: null, Crc32: 0, Peak: 0, Quality: 1.0, Attempts: 1)],
            AccurateRipFound: false,
            SkippedDataTrackCount: 0);
        var logOptions = new EacLogOptions(
            releaseInfo,
            ripResult,
            toc,
            predictedDiscDirectory,
            DevicePath: "/dev/sr1",
            DriveVendor: null,
            DriveModel: null,
            DriveRelease: null,
            ReadOffset: 0,
            Overread: false,
            CacheDefeat: CacheDefeatResult.Unknown,
            CdParanoiaVersion: "unknown",
            CdrdaoVersion: "unknown",
            FlacVersion: "unknown",
            Uname: "unknown",
            OsPrettyName: null,
            StartTime: DateTimeOffset.UtcNow,
            EndTime: DateTimeOffset.UtcNow);
        var logText = WhatinatorEacLog.Format(logOptions);
        var filenameLine = logText.Split('\n').Single(l => l.TrimStart().StartsWith("Filename ", StringComparison.Ordinal));
        var loggedPath = filenameLine.Trim()["Filename ".Length..];

        var packager = new FlacPackager(new FakeCoverArtClient());
        await packager.PackageAsync(new FlacPackageOptions(releaseInfo, sourceDir, destinationParentDirectory));

        Assert.True(File.Exists(loggedPath));
    }

    /// <summary>Creates a fresh destination/source directory pair under this test class's temp directory.</summary>
    private (string DestinationParentDirectory, string SourceDir) CreatePackagingDirs()
    {
        var destinationParentDirectory = Path.Combine(_tempDir, Guid.NewGuid().ToString());
        var sourceDir = Path.Combine(destinationParentDirectory, "source");
        Directory.CreateDirectory(destinationParentDirectory);
        Directory.CreateDirectory(sourceDir);
        return (destinationParentDirectory, sourceDir);
    }

    private static void CreateFakeFlacFile(string dir, string fileName) =>
        File.WriteAllText(Path.Combine(dir, fileName), "fake flac bytes: " + fileName);

    private static ReleaseInfo CreateSingleDiscRelease()
    {
        List<TrackInfo> tracks = [new TrackInfo(1, "Track One", "Artist", TimeSpan.FromSeconds(100))];

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
            Media: [new MediumInfo(1, null, tracks)]);
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
