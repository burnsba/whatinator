namespace Whatinator.Core.Tests;

public class M3uPlaylistTests : IDisposable
{
    private readonly string _tempDir;

    public M3uPlaylistTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-m3u-tests-" + Guid.NewGuid());
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
    public void Write_ProducesExtM3uFormat()
    {
        List<(string RelativePath, string Artist, string Title, int DurationSeconds)> tracks =
        [
            ("01 - Glorilla - Intro.flac", "Glorilla", "Intro", 91),
            ("02 - Glorilla - Hollon.flac", "Glorilla", "Hollon", 131),
        ];
        var m3uPath = Path.Combine(_tempDir, "playlist.m3u");

        M3uPlaylist.Write(tracks, m3uPath);

        var expected = new[]
        {
            "#EXTM3U",
            "#EXTINF:91,Glorilla - Intro",
            "01 - Glorilla - Intro.flac",
            "#EXTINF:131,Glorilla - Hollon",
            "02 - Glorilla - Hollon.flac",
        };
        Assert.Equal(expected, File.ReadAllLines(m3uPath));
    }

    [Fact]
    public void Write_EmptyTrackList_WritesOnlyHeader()
    {
        var m3uPath = Path.Combine(_tempDir, "empty.m3u");

        M3uPlaylist.Write([], m3uPath);

        Assert.Equal(["#EXTM3U"], File.ReadAllLines(m3uPath));
    }
}
