using System.Diagnostics;
using Whatinator.Core.Flac;

namespace Whatinator.Core.Tests;

public class FlacEncoderTests : IDisposable
{
    private readonly string _tempDir;

    public FlacEncoderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-flacencoder-tests-" + Guid.NewGuid());
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
    public void BuildStartInfo_UsesFlacExecutable()
    {
        var options = CreateOptions();

        var startInfo = FlacEncoder.BuildStartInfo(options);

        Assert.Equal("flac", startInfo.FileName);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildStartInfo_PassesVerifyOutputAndTags()
    {
        var options = CreateOptions();

        var args = FlacEncoder.BuildStartInfo(options).ArgumentList;

        Assert.Contains("--verify", args);
        Assert.Contains("-o", args);
        Assert.Equal("out.flac", args[args.IndexOf("-o") + 1]);
        Assert.Contains("ARTIST=Artist", args);
        Assert.Contains("ALBUM=Album", args);
        Assert.Contains("TITLE=Track One", args);
        Assert.Contains("ALBUMARTIST=Album Artist", args);
        Assert.Contains("DATE=1999", args);
        Assert.Contains("TRACKNUMBER=1", args);
        Assert.Contains("TRACKTOTAL=10", args);
        Assert.Contains("GENRE=Rock", args);
        Assert.Equal("in.wav", args[^1]);
    }

    [Fact]
    public void BuildStartInfo_OmitsDateAndGenre_WhenNull()
    {
        var options = CreateOptions() with { Year = null, Genre = null };

        var args = FlacEncoder.BuildStartInfo(options).ArgumentList;

        Assert.DoesNotContain(args, a => a.StartsWith("DATE=", StringComparison.Ordinal));
        Assert.DoesNotContain(args, a => a.StartsWith("GENRE=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EncodeAsync_ProducesValidFlacWithGivenTags()
    {
        var wavPath = Path.Combine(_tempDir, "in.wav");
        var flacPath = Path.Combine(_tempDir, "out.flac");
        CreateSyntheticWav(wavPath);

        var options = new FlacEncodeOptions(
            InputWavPath: wavPath,
            OutputFlacPath: flacPath,
            Title: "Track One",
            Artist: "Artist",
            Album: "Album",
            AlbumArtist: "Album Artist",
            Year: "1999",
            TrackNumber: 3,
            TrackCount: 10,
            Genre: "Rock");

        var encoder = new FlacEncoder();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        var result = await encoder.EncodeAsync(options, stdout, stderr, CancellationToken.None);

        Assert.True(result.Success, $"flac exited {result.ExitCode}: {System.Text.Encoding.UTF8.GetString(stderr.ToArray())}");
        Assert.True(File.Exists(flacPath));
        Assert.True(new FileInfo(flacPath).Length > 0);

        var tags = ReadTags(flacPath);
        Assert.Equal("Track One", tags["TITLE"]);
        Assert.Equal("Artist", tags["ARTIST"]);
        Assert.Equal("Album", tags["ALBUM"]);
        Assert.Equal("Album Artist", tags["album_artist"]);
        Assert.Equal("Rock", tags["GENRE"]);
        Assert.Equal("3", tags["track"]);
    }

    private static Dictionary<string, string> ReadTags(string flacPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffprobe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-v");
        startInfo.ArgumentList.Add("quiet");
        startInfo.ArgumentList.Add("-show_entries");
        startInfo.ArgumentList.Add("format_tags");
        startInfo.ArgumentList.Add("-of");
        startInfo.ArgumentList.Add("default=noprint_wrappers=1");
        startInfo.ArgumentList.Add(flacPath);

        using var process = Process.Start(startInfo) !;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].StartsWith("TAG:", StringComparison.Ordinal))
            .ToDictionary(parts => parts[0]["TAG:".Length..], parts => parts[1]);
    }

    private static FlacEncodeOptions CreateOptions() => new(
        InputWavPath: "in.wav",
        OutputFlacPath: "out.flac",
        Title: "Track One",
        Artist: "Artist",
        Album: "Album",
        AlbumArtist: "Album Artist",
        Year: "1999",
        TrackNumber: 1,
        TrackCount: 10,
        Genre: "Rock");

    private static void CreateSyntheticWav(string path)
    {
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
}
