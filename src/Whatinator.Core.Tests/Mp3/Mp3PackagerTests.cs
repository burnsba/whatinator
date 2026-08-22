using System.Diagnostics;
using Whatinator.Core.Metadata;
using Whatinator.Core.Mp3;

namespace Whatinator.Core.Tests;

/// <summary>
/// Exercises <see cref="Mp3Packager"/> against the real <c>lame</c> binary
/// (available on the dev machine) rather than mocking it -- encoding a
/// handful of tiny synthetic FLAC files is fast, and the whole point is
/// verifying the actual folder layout <c>lame</c> ends up producing.
/// </summary>
public class Mp3PackagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sourceDir;
    private readonly string _destDir;

    public Mp3PackagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-mp3packager-tests-" + Guid.NewGuid());
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
    public async Task PackageAsync_SingleDisc_EncodesFilesAndWritesArtifacts()
    {
        CreateFakeFlac(_sourceDir, "01. Artist - Track One.flac");
        CreateFakeFlac(_sourceDir, "02. Artist - Track Two.flac");

        var releaseInfo = CreateSingleDiscRelease();
        var packager = new Mp3Packager();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        var result = await packager.PackageAsync(new Mp3PackageOptions(releaseInfo, _sourceDir, _destDir), stdout, stderr);

        Assert.Equal(2, result.EncodedTrackCount);
        Assert.Equal(result.ContainerDirectory, result.DiscDirectory);
        Assert.Equal("Artist - Album [mp3 v0 2000]", Path.GetFileName(result.ContainerDirectory));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "01. Artist - Track One.mp3")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "02. Artist - Track Two.mp3")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "releaseinfo.json")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "id.txt")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "checksum_sha256.txt")));
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "Artist - Album.m3u")));
        Assert.True(File.Exists(result.LogFilePath));
        var logText = File.ReadAllText(result.LogFilePath);
        Assert.Contains("Quality: VBR -V0", logText, StringComparison.Ordinal);
        Assert.Contains("Track 1 of 2: Track 1\n", logText, StringComparison.Ordinal);
        Assert.Contains("Track 2 of 2: Track 2\n", logText, StringComparison.Ordinal);
        Assert.Contains("LAME", logText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PackageAsync_MultiDisc_RequiresDiscNumber()
    {
        var releaseInfo = CreateMultiDiscRelease();
        var packager = new Mp3Packager();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(
            () => packager.PackageAsync(new Mp3PackageOptions(releaseInfo, _sourceDir, _destDir), stdout, stderr));
    }

    [Fact]
    public async Task PackageAsync_MultiDisc_RejectsOutOfRangeDiscNumber()
    {
        var releaseInfo = CreateMultiDiscRelease();
        var packager = new Mp3Packager();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(
            () => packager.PackageAsync(new Mp3PackageOptions(releaseInfo, _sourceDir, _destDir, DiscNumber: 3), stdout, stderr));
    }

    [Fact]
    public async Task PackageAsync_MultiDisc_PackagesBothDiscsAcrossTwoCalls_M3uCoversBoth()
    {
        var releaseInfo = CreateMultiDiscRelease();
        var packager = new Mp3Packager();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        var disc1Source = Path.Combine(_tempDir, "source-disc1");
        Directory.CreateDirectory(disc1Source);
        CreateFakeFlac(disc1Source, "01. Artist - D1 Track One.flac");
        CreateFakeFlac(disc1Source, "02. Artist - D1 Track Two.flac");
        var result1 = await packager.PackageAsync(
            new Mp3PackageOptions(releaseInfo, disc1Source, _destDir, DiscNumber: 1), stdout, stderr);

        var m3uPath = Path.Combine(result1.ContainerDirectory, "Artist - Album.m3u");
        var afterDisc1 = File.ReadAllLines(m3uPath);
        Assert.Equal(5, afterDisc1.Length); // header + 2 tracks * 2 lines each

        var disc2Source = Path.Combine(_tempDir, "source-disc2");
        Directory.CreateDirectory(disc2Source);
        CreateFakeFlac(disc2Source, "01. Artist - D2 Track One.flac");
        CreateFakeFlac(disc2Source, "02. Artist - D2 Track Two.flac");
        var result2 = await packager.PackageAsync(
            new Mp3PackageOptions(releaseInfo, disc2Source, _destDir, DiscNumber: 2), stdout, stderr);

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
        var releaseInfo = CreateSingleDiscRelease();
        var packager = new Mp3Packager();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => packager.PackageAsync(new Mp3PackageOptions(releaseInfo, _sourceDir, _destDir), stdout, stderr));
    }

    [Fact]
    public async Task PackageAsync_ThrowsWhenNoFlacFileMatchesATrackNumber()
    {
        CreateFakeFlac(_sourceDir, "not-a-track-number.flac");
        var releaseInfo = CreateSingleDiscRelease(trackCount: 2);
        var packager = new Mp3Packager();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => packager.PackageAsync(new Mp3PackageOptions(releaseInfo, _sourceDir, _destDir), stdout, stderr));
    }

    [Fact]
    public async Task PackageAsync_DegradedRip_EncodesOnlyTheTracksThatWereCaptured()
    {
        CreateFakeFlac(_sourceDir, "01. Artist - Track One.flac");
        var releaseInfo = CreateSingleDiscRelease(trackCount: 2);
        var packager = new Mp3Packager();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        var result = await packager.PackageAsync(new Mp3PackageOptions(releaseInfo, _sourceDir, _destDir), stdout, stderr);

        Assert.Equal(1, result.EncodedTrackCount);
        Assert.True(File.Exists(Path.Combine(result.ContainerDirectory, "01. Artist - Track One.mp3")));
        Assert.False(File.Exists(Path.Combine(result.ContainerDirectory, "02. Artist - Track Two.mp3")));
    }

    [Fact]
    public async Task PackageAsync_CopiesCoverArt_WhenPresentInFlacFolder()
    {
        CreateFakeFlac(_sourceDir, "01. Artist - Track One.flac");
        CreateSyntheticJpeg(Path.Combine(_sourceDir, "cover.jpg"));

        var releaseInfo = CreateSingleDiscRelease(trackCount: 1);
        var packager = new Mp3Packager();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        var result = await packager.PackageAsync(new Mp3PackageOptions(releaseInfo, _sourceDir, _destDir), stdout, stderr);

        Assert.NotNull(result.CoverArtPath);
        Assert.True(File.Exists(result.CoverArtPath));
        Assert.Equal("cover.jpg", Path.GetFileName(result.CoverArtPath));
    }

    [Fact]
    public async Task PackageAsync_NoCoverArtInFlacFolder_ProducesNoCoverArt()
    {
        CreateFakeFlac(_sourceDir, "01. Artist - Track One.flac");
        var releaseInfo = CreateSingleDiscRelease(trackCount: 1);
        var packager = new Mp3Packager();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        var result = await packager.PackageAsync(new Mp3PackageOptions(releaseInfo, _sourceDir, _destDir), stdout, stderr);

        Assert.Null(result.CoverArtPath);
        Assert.Empty(Directory.GetFiles(result.ContainerDirectory, "cover.*"));
    }

    private static void CreateFakeFlac(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        var startInfo = new ProcessStartInfo
        {
            FileName = "sox",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add("44100");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add("synth");
        startInfo.ArgumentList.Add("0.2");
        startInfo.ArgumentList.Add("sine");
        startInfo.ArgumentList.Add("440");

        using var process = Process.Start(startInfo) !;
        process.WaitForExit();
    }

    private static void CreateSyntheticJpeg(string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "magick",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-size");
        startInfo.ArgumentList.Add("16x16");
        startInfo.ArgumentList.Add("xc:red");
        startInfo.ArgumentList.Add(path);

        using var process = Process.Start(startInfo) !;
        process.WaitForExit();
    }

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
