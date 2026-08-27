using System.Diagnostics;
using Whatinator.Core.Mp3;

namespace Whatinator.Core.Tests;

public class LameEncoderTests : IDisposable
{
    private readonly string _tempDir;

    public LameEncoderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-lame-tests-" + Guid.NewGuid());
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
    public void BuildStartInfo_UsesLameExecutable()
    {
        var options = CreateOptions();

        var startInfo = LameEncoder.BuildStartInfo(options);

        Assert.Equal("lame", startInfo.FileName);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildStartInfo_PassesV0AndTags()
    {
        var options = CreateOptions();

        var args = LameEncoder.BuildStartInfo(options).ArgumentList;

        Assert.Equal("-V0", args[0]);
        Assert.Contains("--tt", args);
        Assert.Equal("Track One", args[args.IndexOf("--tt") + 1]);
        Assert.Contains("--ta", args);
        Assert.Equal("Artist", args[args.IndexOf("--ta") + 1]);
        Assert.Contains("--tl", args);
        Assert.Equal("Album", args[args.IndexOf("--tl") + 1]);
        Assert.Contains("--tv", args);
        Assert.Equal("TPE2=Album Artist", args[args.IndexOf("--tv") + 1]);
        Assert.Contains("--ty", args);
        Assert.Equal("1999", args[args.IndexOf("--ty") + 1]);
        Assert.Contains("--tn", args);
        Assert.Equal("1/10", args[args.IndexOf("--tn") + 1]);
        Assert.Contains("--tg", args);
        Assert.Equal("Rock", args[args.IndexOf("--tg") + 1]);
        Assert.Equal("in.flac", args[^2]);
        Assert.Equal("out.mp3", args[^1]);
    }

    [Fact]
    public void BuildStartInfo_OmitsYearAndGenre_WhenNull()
    {
        var options = CreateOptions() with { Year = null, Genre = null };

        var args = LameEncoder.BuildStartInfo(options).ArgumentList;

        Assert.DoesNotContain("--ty", args);
        Assert.DoesNotContain("--tg", args);
    }

    [Fact]
    public void BuildStartInfo_NeverEmbedsCoverArt()
    {
        var options = CreateOptions();

        var args = LameEncoder.BuildStartInfo(options).ArgumentList;

        Assert.DoesNotContain("--ti", args);
    }

    [Fact]
    public async Task EncodeAsync_ProducesValidMp3WithGivenTags()
    {
        var flacPath = Path.Combine(_tempDir, "in.flac");
        var mp3Path = Path.Combine(_tempDir, "out.mp3");
        CreateSyntheticFlac(flacPath);

        var options = new LameEncodeOptions(
            InputFlacPath: flacPath,
            OutputMp3Path: mp3Path,
            Title: "Track One",
            Artist: "Artist",
            Album: "Album",
            AlbumArtist: "Album Artist",
            Year: "1999",
            TrackNumber: 1,
            TrackCount: 10,
            Genre: "Rock");

        var encoder = new LameEncoder();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        var result = await encoder.EncodeAsync(options, stdout, stderr, CancellationToken.None);

        Assert.True(result.Success, $"lame exited {result.ExitCode}: {System.Text.Encoding.UTF8.GetString(stderr.ToArray())}");
        Assert.True(File.Exists(mp3Path));
        Assert.True(new FileInfo(mp3Path).Length > 0);

        // TPE2 (album artist) is written UTF-16-encoded, so a raw byte/string
        // scan won't find it directly -- read tags back structurally via
        // ffprobe instead (same tool named in phase-007.md's demo plan for
        // spot-checking encoder output).
        var tags = ReadTags(mp3Path);
        Assert.Equal("Track One", tags["title"]);
        Assert.Equal("Artist", tags["artist"]);
        Assert.Equal("Album", tags["album"]);
        Assert.Equal("Album Artist", tags["album_artist"]);
        Assert.Equal("Rock", tags["genre"]);
    }

    [Fact]
    public async Task EncodeAsync_CapturesLameOutput_ForTheMp3Log()
    {
        var flacPath = Path.Combine(_tempDir, "in.flac");
        var mp3Path = Path.Combine(_tempDir, "out.mp3");
        CreateSyntheticFlac(flacPath);

        var encoder = new LameEncoder();
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        var result = await encoder.EncodeAsync(CreateOptions() with { InputFlacPath = flacPath, OutputMp3Path = mp3Path }, stdout, stderr, CancellationToken.None);

        var rawOutput = System.Text.Encoding.UTF8.GetString(stderr.ToArray());

        Assert.True(result.Success);

        // CapturedOutput is LameOutputFilter.ExtractSummary's output, not
        // lame's raw stderr (see LameOutputFilter for why): the live
        // progress banner/redraws are gone, but the final summary survives.
        Assert.NotEqual(rawOutput, result.CapturedOutput);
        Assert.DoesNotContain('\x1B', result.CapturedOutput);
        Assert.Contains("kbps", result.CapturedOutput, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> ReadTags(string mp3Path)
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
        startInfo.ArgumentList.Add(mp3Path);

        using var process = Process.Start(startInfo) !;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].StartsWith("TAG:", StringComparison.Ordinal))
            .ToDictionary(parts => parts[0]["TAG:".Length..], parts => parts[1]);
    }

    private static LameEncodeOptions CreateOptions() => new(
        InputFlacPath: "in.flac",
        OutputMp3Path: "out.mp3",
        Title: "Track One",
        Artist: "Artist",
        Album: "Album",
        AlbumArtist: "Album Artist",
        Year: "1999",
        TrackNumber: 1,
        TrackCount: 10,
        Genre: "Rock");

    private static void CreateSyntheticFlac(string path)
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
