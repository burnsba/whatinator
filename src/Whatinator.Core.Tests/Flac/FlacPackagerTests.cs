using Whatinator.Core.CoverArt;
using Whatinator.Core.Flac;
using Whatinator.Core.Metadata;

namespace Whatinator.Core.Tests;

public class FlacPackagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sourceDir;
    private readonly string _destDir;

    public FlacPackagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-flacpackager-tests-" + Guid.NewGuid());
        _sourceDir = Path.Combine(_tempDir, "source");
        _destDir = Path.Combine(_tempDir, "dest");
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_destDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task PackageAsync_SingleDisc_MovesFilesAndWritesArtifacts()
    {
        CreateFakeTrack(_sourceDir, "01. Artist - Track One.flac");
        CreateFakeTrack(_sourceDir, "02. Artist - Track Two.flac");
        CreateFakeLog(_sourceDir, "Artist - Album.log", "sample rip log content");

        var releaseInfo = CreateSingleDiscRelease();
        var packager = new FlacPackager(new FakeCoverArtClient());

        var result = await packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir));

        Assert.Equal(2, result.MovedFlacFileCount);
        Assert.Equal(result.ContainerDirectory, result.DiscDirectory);
        Assert.True(Directory.Exists(result.ContainerDirectory));
        Assert.Equal("Artist - Album [flac 2000]", Path.GetFileName(result.ContainerDirectory));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "01. Artist - Track One.flac")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "02. Artist - Track Two.flac")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "releaseinfo.json")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "id.txt")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "checksum_sha256.txt")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "Artist - Album.m3u")));
        Assert.Empty(Directory.GetFiles(_sourceDir));
    }

    [Fact]
    public async Task PackageAsync_WritesUpcIntoIdTxt_WhenDiscCatalogNumberProvided()
    {
        CreateFakeTrack(_sourceDir, "01. Artist - Track One.flac");
        var releaseInfo = CreateSingleDiscRelease(trackCount: 1);
        var packager = new FlacPackager(new FakeCoverArtClient());

        var result = await packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir, DiscCatalogNumber: "602475160991"));

        var idText = File.ReadAllText(Path.Combine(result.ContainerDirectory, "id.txt"));
        Assert.Contains("upc: 602475160991\n", idText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PackageAsync_PreservesLogContentByteForByte()
    {
        CreateFakeTrack(_sourceDir, "01. Artist - Track One.flac");
        const string logContent = "Log created by: whatinator\n==== Log checksum ABCDEF ====";
        CreateFakeLog(_sourceDir, "Artist - Album.log", logContent);

        var releaseInfo = CreateSingleDiscRelease(trackCount: 1);
        var packager = new FlacPackager(new FakeCoverArtClient());

        var result = await packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir));

        Assert.NotNull(result.LogFilePath);
        Assert.Equal(logContent, File.ReadAllText(result.LogFilePath));
    }

    [Fact]
    public async Task PackageAsync_MultiDisc_RequiresDiscNumber()
    {
        var releaseInfo = CreateMultiDiscRelease();
        var packager = new FlacPackager(new FakeCoverArtClient());

        await Assert.ThrowsAsync<ArgumentException>(
            () => packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir)));
    }

    [Fact]
    public async Task PackageAsync_MultiDisc_RejectsOutOfRangeDiscNumber()
    {
        var releaseInfo = CreateMultiDiscRelease();
        var packager = new FlacPackager(new FakeCoverArtClient());

        await Assert.ThrowsAsync<ArgumentException>(
            () => packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir, DiscNumber: 3)));
    }

    [Fact]
    public async Task PackageAsync_MultiDisc_PackagesBothDiscsAcrossTwoCalls_M3uCoversBoth()
    {
        var releaseInfo = CreateMultiDiscRelease();
        var packager = new FlacPackager(new FakeCoverArtClient());

        var disc1Source = Path.Combine(_tempDir, "source-disc1");
        Directory.CreateDirectory(disc1Source);
        CreateFakeTrack(disc1Source, "01. Artist - D1 Track One.flac");
        CreateFakeTrack(disc1Source, "02. Artist - D1 Track Two.flac");
        CreateFakeLog(disc1Source, "disc1.log", "disc 1 log");
        var result1 = await packager.PackageAsync(
            new FlacPackageOptions(releaseInfo, disc1Source, _destDir, DiscNumber: 1));

        var m3uPath = Path.Combine(result1.ContainerDirectory, "Artist - Album.m3u");
        var afterDisc1 = File.ReadAllLines(m3uPath);
        Assert.Equal(5, afterDisc1.Length); // header + 2 tracks * 2 lines each

        var disc2Source = Path.Combine(_tempDir, "source-disc2");
        Directory.CreateDirectory(disc2Source);
        CreateFakeTrack(disc2Source, "01. Artist - D2 Track One.flac");
        CreateFakeTrack(disc2Source, "02. Artist - D2 Track Two.flac");
        CreateFakeLog(disc2Source, "disc2.log", "disc 2 log");
        var result2 = await packager.PackageAsync(
            new FlacPackageOptions(releaseInfo, disc2Source, _destDir, DiscNumber: 2));

        Assert.Equal(result1.ContainerDirectory, result2.ContainerDirectory);
        Assert.NotEqual(result1.DiscDirectory, result2.DiscDirectory);
        Assert.Equal("cd1", Path.GetFileName(result1.DiscDirectory));
        Assert.Equal("cd2", Path.GetFileName(result2.DiscDirectory));

        var afterDisc2 = File.ReadAllLines(m3uPath);
        Assert.Equal(9, afterDisc2.Length); // header + 2 discs * 2 tracks * 2 lines each
        Assert.Contains(afterDisc2, l => l.Contains("cd1/", StringComparison.Ordinal));
        Assert.Contains(afterDisc2, l => l.Contains("cd2/", StringComparison.Ordinal));

        var checksumLines = File.ReadAllLines(Path.Combine(result1.ContainerDirectory, "checksum_sha256.txt"));
        Assert.Equal(4, checksumLines.Length);
    }

    [Fact]
    public async Task PackageAsync_ThrowsWhenNoFlacFiles()
    {
        CreateFakeLog(_sourceDir, "log.log", "content");
        var releaseInfo = CreateSingleDiscRelease();
        var packager = new FlacPackager(new FakeCoverArtClient());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir)));
    }

    [Fact]
    public async Task PackageAsync_LogFilePathIsNull_WhenNoLogFile()
    {
        // WhatinatorRipRunner (phase 015) doesn't produce a .log yet -- phase
        // 016 adds the EAC-style rip log. Packaging must still succeed.
        CreateFakeTrack(_sourceDir, "01. Artist - Track One.flac");
        var releaseInfo = CreateSingleDiscRelease(trackCount: 1);
        var packager = new FlacPackager(new FakeCoverArtClient());

        var result = await packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir));

        Assert.Null(result.LogFilePath);
    }

    [Fact]
    public async Task PackageAsync_MovesWavFiles_WhenPresent()
    {
        CreateFakeTrack(_sourceDir, "01. Artist - Track One.flac");
        File.WriteAllText(Path.Combine(_sourceDir, "01. Artist - Track One.wav"), "fake wav bytes");
        var releaseInfo = CreateSingleDiscRelease(trackCount: 1);
        var packager = new FlacPackager(new FakeCoverArtClient());

        var result = await packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir));

        Assert.True(File.Exists(Path.Combine(result.DiscDirectory, "01. Artist - Track One.wav")));
        Assert.Empty(Directory.GetFiles(_sourceDir));
    }

    [Fact]
    public async Task PackageAsync_ThrowsWhenMultipleLogFiles()
    {
        CreateFakeTrack(_sourceDir, "01. Artist - Track One.flac");
        CreateFakeLog(_sourceDir, "a.log", "a");
        CreateFakeLog(_sourceDir, "b.log", "b");
        var releaseInfo = CreateSingleDiscRelease(trackCount: 1);
        var packager = new FlacPackager(new FakeCoverArtClient());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir)));
    }

    [Fact]
    public async Task PackageAsync_FetchesCoverArt_WhenAvailable()
    {
        CreateFakeTrack(_sourceDir, "01. Artist - Track One.flac");
        CreateFakeLog(_sourceDir, "log.log", "content");
        var releaseInfo = CreateSingleDiscRelease(trackCount: 1);
        var coverArtClient = new FakeCoverArtClient(new CoverArtResult([1, 2, 3], ".jpg"));
        var packager = new FlacPackager(coverArtClient);

        var result = await packager.PackageAsync(new FlacPackageOptions(releaseInfo, _sourceDir, _destDir));

        Assert.NotNull(result.CoverArtPath);
        Assert.True(File.Exists(result.CoverArtPath));
        Assert.Equal(1, coverArtClient.CallCount);
    }

    [Fact]
    public async Task PackageAsync_SkipsCoverArtFetch_WhenAlreadyPresent()
    {
        CreateFakeTrack(_sourceDir, "01. Artist - Track One.flac");
        CreateFakeLog(_sourceDir, "log.log", "content");
        var releaseInfo = CreateSingleDiscRelease();
        var coverArtClient = new FakeCoverArtClient(new CoverArtResult([1, 2, 3], ".jpg"));
        var packager = new FlacPackager(coverArtClient);

        var options = new FlacPackageOptions(releaseInfo, _sourceDir, _destDir);
        await packager.PackageAsync(options);
        Assert.Equal(1, coverArtClient.CallCount);

        // Simulate a second packaging pass (e.g. a retry) by repopulating the source.
        CreateFakeTrack(_sourceDir, "02. Artist - Track Two.flac");
        CreateFakeLog(_sourceDir, "log2.log", "content2");
        await packager.PackageAsync(options);

        Assert.Equal(1, coverArtClient.CallCount);
    }

    private static void CreateFakeTrack(string dir, string fileName) =>
        File.WriteAllText(Path.Combine(dir, fileName), "fake flac bytes: " + fileName);

    private static void CreateFakeLog(string dir, string fileName, string content) =>
        File.WriteAllText(Path.Combine(dir, fileName), content);

    private static ReleaseInfo CreateSingleDiscRelease(int trackCount = 2)
    {
        List<TrackInfo> tracks = [];
        for (var i = 1; i <= trackCount; i++)
        {
            tracks.Add(new TrackInfo(i, $"Track {i}", "Artist", TimeSpan.FromSeconds(100 + i)));
        }

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
        List<TrackInfo> disc1Tracks =
        [
            new TrackInfo(1, "D1 Track One", "Artist", TimeSpan.FromSeconds(100)),
            new TrackInfo(2, "D1 Track Two", "Artist", TimeSpan.FromSeconds(101)),
        ];
        List<TrackInfo> disc2Tracks =
        [
            new TrackInfo(1, "D2 Track One", "Artist", TimeSpan.FromSeconds(102)),
            new TrackInfo(2, "D2 Track Two", "Artist", TimeSpan.FromSeconds(103)),
        ];

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
            Media: [new MediumInfo(1, "Disc One", disc1Tracks), new MediumInfo(2, "Disc Two", disc2Tracks)]);
    }
}
