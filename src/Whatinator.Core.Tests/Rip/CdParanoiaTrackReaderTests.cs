using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class CdParanoiaTrackReaderTests
{
    private const int WordsPerFrame = 588 * 2;

    private static readonly DiscToc SingleTrackToc = new([new DiscTocTrack(1, 32, 407, IsAudio: true)]);

    [Fact]
    public void BuildStartInfo_UsesCdParanoiaExecutable()
    {
        var options = new CdParanoiaTrackOptions("/dev/sr1", SingleTrackToc, 1, "/tmp/out/track01.wav");

        var startInfo = CdParanoiaTrackReader.BuildStartInfo(options, "/tmp/out/scratch.wav");

        Assert.Equal("cd-paranoia", startInfo.FileName);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildStartInfo_PassesOffsetDeviceAndSpan()
    {
        var options = new CdParanoiaTrackOptions("/dev/sr1", SingleTrackToc, 1, "/tmp/out/track01.wav", Offset: 6);

        var args = CdParanoiaTrackReader.BuildStartInfo(options, "/tmp/out/scratch.wav").ArgumentList;

        Assert.Equal(
            ["--stderr-progress", "--sample-offset=6", "--force-cdrom-device", "/dev/sr1", "1[00:00:00.00]-1[00:00:05.00]", "/tmp/out/scratch.wav"],
            args);
    }

    [Fact]
    public void BuildStartInfo_PassesForceOverread_WhenSet()
    {
        var options = new CdParanoiaTrackOptions("/dev/sr1", SingleTrackToc, 1, "/tmp/out/track01.wav", Overread: true);

        var args = CdParanoiaTrackReader.BuildStartInfo(options, "/tmp/out/scratch.wav").ArgumentList;

        Assert.Contains("--force-overread", args);
    }

    [Fact]
    public void BuildStartInfo_OmitsForceOverread_WhenNotSet()
    {
        var options = new CdParanoiaTrackOptions("/dev/sr1", SingleTrackToc, 1, "/tmp/out/track01.wav");

        var args = CdParanoiaTrackReader.BuildStartInfo(options, "/tmp/out/scratch.wav").ArgumentList;

        Assert.DoesNotContain("--force-overread", args);
    }

    [Fact]
    public void BuildStartInfo_UsesTheSecondTrackSSpan_WhenReadingTrack2()
    {
        var toc = new DiscToc([
            new DiscTocTrack(1, 32, 16746, IsAudio: true),
            new DiscTocTrack(2, 16747, 33851, IsAudio: true),
        ]);
        var options = new CdParanoiaTrackOptions("/dev/sr1", toc, 2, "/tmp/out/track02.wav");

        var args = CdParanoiaTrackReader.BuildStartInfo(options, "/tmp/out/scratch.wav").ArgumentList;

        Assert.Equal("2[00:00:00.00]-2[00:03:48.04]", args[^2]);
    }

    [Theory]
    [InlineData(0, "00:00:00.00")]
    [InlineData(74, "00:00:00.74")]
    [InlineData(75, "00:00:01.00")]
    [InlineData(375, "00:00:05.00")]
    [InlineData(4500, "00:01:00.00")]
    [InlineData(16715, "00:03:42.65")] // cross-checked against a real disc's track 1 length ("length 16715 [03:42.65]" -- see phase 013's live cd-paranoia -Q output).
    public void FramesToHmsf_FormatsCorrectly(int frames, string expected)
    {
        Assert.Equal(expected, CdParanoiaTrackReader.FramesToHmsf(frames));
    }

    [Fact]
    public void ParsePeakLevel_ParsesRealSoxStatsOutput()
    {
        const string soxOutput = """
                     Overall     Left      Right
            DC offset          0         0         0
            Min level     -16385    -16385    -16385
            Max level      16385     16385     16385
            Pk lev dB      -6.02     -6.02     -6.02
            """;

        Assert.Equal(16385, CdParanoiaTrackReader.ParsePeakLevel(soxOutput));
    }

    [Fact]
    public void ParsePeakLevel_ReturnsNull_WhenOutputIsUnparseable()
    {
        Assert.Null(CdParanoiaTrackReader.ParsePeakLevel("sox: unrecognized option"));
    }

    [Fact]
    public void ComputeQuality_ReturnsOne_WhenEveryFrameIsReadExactlyTwice()
    {
        // 10-frame track, each read line advances one frame; repeating the
        // same forward pass twice mirrors cdparanoia's normal read+verify
        // behavior and pushes well past the "each frame read twice" bar.
        var lines = string.Join('\n', Enumerable.Range(0, 10).Select(i => $"##: 0 [read] @ {(i + 1) * WordsPerFrame}"));
        var capturedTwice = lines + "\n" + lines;

        Assert.Equal(1.0, CdParanoiaTrackReader.ComputeQuality(capturedTwice, 0, 9));
    }

    [Fact]
    public void ComputeQuality_ReturnsNull_WhenNoReadLinesAreParsed()
    {
        Assert.Null(CdParanoiaTrackReader.ComputeQuality("no progress lines here", 0, 9));
    }

    [Fact]
    public void ComputeQuality_IgnoresNonReadFunctions()
    {
        Assert.Null(CdParanoiaTrackReader.ComputeQuality("##: 14 [wrote] @ 1176\n##: 1 [verify] @ 1176", 0, 9));
    }

    [Fact]
    public void ComputeQuality_ConvertsAbsoluteOffsetsToTrackRelative_ForNonZeroTrackStart()
    {
        // Mirrors a non-first track: cd-paranoia's "@ <wordOffset>" values are
        // absolute disc frames, so every line here is offset by trackStartFrame
        // (6835, as in the Glorilla track-2 case from the backlog item) and
        // ComputeQuality must subtract it back out before comparing against the
        // track-relative start/stop bounds, exactly as CdParanoiaProgressReporter.Feed
        // does. Without that subtraction, the very first line's huge apparent
        // forward jump saturates "reads" at frameCount and quality reads 1.0.
        const int trackStartFrame = 6835;

        // A clean forward+verify double pass over the whole 10-frame track.
        var cleanPass = string.Join(
            '\n',
            Enumerable.Range(0, 10).Select(i => $"##: 0 [read] @ {(trackStartFrame + i + 1) * WordsPerFrame}"));

        // A partial third pass re-reading frames 0-4 (a rewind, then forward
        // progress that doesn't reach the end) -- representative of a real
        // re-read that wastes reads without advancing "furthest read".
        var partialRereadPass = string.Join(
            '\n',
            Enumerable.Range(0, 5).Select(i => $"##: 0 [read] @ {(trackStartFrame + i) * WordsPerFrame}"));

        var captured = cleanPass + "\n" + cleanPass + "\n" + partialRereadPass;

        var quality = CdParanoiaTrackReader.ComputeQuality(captured, 0, 9, trackStartFrame);

        Assert.NotNull(quality);
        Assert.True(quality < 1.0, $"expected quality below 1.0 for re-read input, got {quality}");
    }

    [Fact]
    public async Task RetryAsync_SucceedsImmediately_WhenFirstAttemptMatches()
    {
        var (success, attempts) = await CdParanoiaTrackReader.RetryAsync(5, _ => Task.FromResult(true), CancellationToken.None);

        Assert.True(success);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task RetryAsync_SucceedsAfterMismatches_WithoutExhaustingRetries()
    {
        var callCount = 0;
        Task<bool> Attempt(CancellationToken ct)
        {
            callCount++;
            return Task.FromResult(callCount >= 3);
        }

        var (success, attempts) = await CdParanoiaTrackReader.RetryAsync(5, Attempt, CancellationToken.None);

        Assert.True(success);
        Assert.Equal(3, attempts);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task RetryAsync_ReturnsDegraded_WhenEveryAttemptMismatches()
    {
        var callCount = 0;
        Task<bool> Attempt(CancellationToken ct)
        {
            callCount++;
            return Task.FromResult(false);
        }

        var (success, attempts) = await CdParanoiaTrackReader.RetryAsync(5, Attempt, CancellationToken.None);

        Assert.False(success);
        Assert.Equal(5, attempts);
        Assert.Equal(5, callCount);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Result_Degraded_ReflectsMatched(bool matched, bool expectedDegraded)
    {
        var result = new CdParanoiaTrackResult(matched, matched ? "/tmp/out.wav" : null, matched ? 123u : null, null, null, 1);

        Assert.Equal(expectedDegraded, result.Degraded);
    }
}
