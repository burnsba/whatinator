using Whatinator.Core.Metadata;

namespace Whatinator.Core.Tests;

public class ReleasePackageArtifactsTests : IDisposable
{
    private readonly string _tempDir;

    public ReleasePackageArtifactsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-releasepackageartifacts-tests-" + Guid.NewGuid());
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
    public void Write_CalledTwice_ProducesIdenticalOutput()
    {
        File.WriteAllText(Path.Combine(_tempDir, "01. Artist - Track One.flac"), "fake flac bytes");
        File.WriteAllText(Path.Combine(_tempDir, "02. Artist - Track Two.flac"), "fake flac bytes 2");
        var releaseInfo = CreateSingleDiscRelease();

        ReleasePackageArtifacts.Write(releaseInfo, _tempDir, isMultiDisc: false, ".flac");
        var checksumAfterFirst = File.ReadAllText(Path.Combine(_tempDir, "checksum_sha256.txt"));
        var m3uAfterFirst = File.ReadAllText(Path.Combine(_tempDir, "Artist - Album.m3u"));
        var idTextAfterFirst = File.ReadAllText(Path.Combine(_tempDir, "id.txt"));

        ReleasePackageArtifacts.Write(releaseInfo, _tempDir, isMultiDisc: false, ".flac");
        var checksumAfterSecond = File.ReadAllText(Path.Combine(_tempDir, "checksum_sha256.txt"));
        var m3uAfterSecond = File.ReadAllText(Path.Combine(_tempDir, "Artist - Album.m3u"));
        var idTextAfterSecond = File.ReadAllText(Path.Combine(_tempDir, "id.txt"));

        Assert.Equal(checksumAfterFirst, checksumAfterSecond);
        Assert.Equal(m3uAfterFirst, m3uAfterSecond);
        Assert.Equal(idTextAfterFirst, idTextAfterSecond);
    }

    [Fact]
    public void WritePlaylist_DegradedDisc_StillContributesPresentTracks()
    {
        // Only track 1's file is present -- simulates a degraded rip that
        // exhausted retries on track 2 (see root CLAUDE.md § "Degraded is
        // not failed").
        File.WriteAllText(Path.Combine(_tempDir, "01. Artist - Track One.flac"), "fake flac bytes");
        var releaseInfo = CreateSingleDiscRelease(trackCount: 2);

        ReleasePackageArtifacts.WritePlaylist(releaseInfo, _tempDir, isMultiDisc: false, ".flac");

        var lines = File.ReadAllLines(Path.Combine(_tempDir, "Artist - Album.m3u"));
        Assert.Equal(3, lines.Length); // header + 1 track * 2 lines
        Assert.Contains(lines, l => l.Contains("Track One", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("Track Two", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteChecksums_OnlyHashesGivenExtensionAndLogFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "01. Artist - Track One.mp3"), "fake mp3 bytes");
        File.WriteAllText(Path.Combine(_tempDir, "Artist - Album.log"), "log content");
        File.WriteAllText(Path.Combine(_tempDir, "id.txt"), "not hashed");

        var count = ReleasePackageArtifacts.WriteChecksums(_tempDir, ".mp3");

        Assert.Equal(2, count);
        var manifestText = File.ReadAllText(Path.Combine(_tempDir, "checksum_sha256.txt"));
        Assert.Contains(".mp3", manifestText, StringComparison.Ordinal);
        Assert.Contains(".log", manifestText, StringComparison.Ordinal);
        Assert.DoesNotContain("id.txt", manifestText, StringComparison.Ordinal);
    }

    private static ReleaseInfo CreateSingleDiscRelease(int trackCount = 2)
    {
        List<TrackInfo> tracks = [];
        for (var i = 1; i <= trackCount; i++)
        {
            tracks.Add(new TrackInfo(i, i == 1 ? "Track One" : "Track Two", "Artist", TimeSpan.FromSeconds(100 + i)));
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
}
