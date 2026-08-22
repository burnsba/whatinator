using System.Buffers.Binary;
using System.Diagnostics;
using Whatinator.Core.AccurateRip;

namespace Whatinator.Core.Tests;

/// <summary>
/// The synthetic-data cases below are change-detectors, not ground truth --
/// they pin the trim math against hand-computed values from a
/// reproducible-in-any-language sample generator, so a regression is
/// obvious, but they cannot tell you whether the trim boundaries are
/// actually correct per the real AccurateRip database. That's what
/// <see cref="Compute_RealTrack1Clip_MatchesIndependentlyComputedChecksum"/>
/// is for.
/// </summary>
public class AccurateRipChecksumTests
{
    [Fact]
    public void Compute_MiddleTrack_UsesFullSampleRange()
    {
        var pcm = GenerateSamples(20);

        var (v1, v2) = AccurateRipChecksum.Compute(pcm, trackNumber: 2, totalTracks: 5);

        Assert.Equal(0xc1ee4656u, v1);
        Assert.Equal(0xc1ee46b2u, v2);
    }

    [Fact]
    public void Compute_FirstTrack_TrimsLeadingFiveSectors()
    {
        var pcm = GenerateSamples(2945);

        var (v1, v2) = AccurateRipChecksum.Compute(pcm, trackNumber: 1, totalTracks: 5);

        Assert.Equal(0xff731d6fu, v1);
        Assert.Equal(0xff7338e6u, v2);
    }

    [Fact]
    public void Compute_LastTrack_TrimsTrailingFiveSectors()
    {
        var pcm = GenerateSamples(2945);

        var (v1, v2) = AccurateRipChecksum.Compute(pcm, trackNumber: 5, totalTracks: 5);

        Assert.Equal(0xfdeb2507u, v1);
        Assert.Equal(0xfdeb250au, v2);
    }

    [Fact]
    public void Compute_SingleTrackDisc_TrimsBothEnds()
    {
        var pcm = GenerateSamples(6000);

        var (v1, v2) = AccurateRipChecksum.Compute(pcm, trackNumber: 1, totalTracks: 1);

        Assert.Equal(0xc571d3a4u, v1);
        Assert.Equal(0xc5749664u, v2);
    }

    [Fact]
    public void Compute_FirstTrackShorterThanTrim_ContributesNothing()
    {
        var pcm = GenerateSamples(100);

        var (v1, v2) = AccurateRipChecksum.Compute(pcm, trackNumber: 1, totalTracks: 5);

        Assert.Equal(0u, v1);
        Assert.Equal(0u, v2);
    }

    /// <summary>
    /// Ground truth: a real 15-second clip from the very start of track 1 of
    /// a real 9-track disc (ripped live in <c>/dev/sr1</c> on 2026-08-22 at
    /// the drive's confirmed +6-sample offset), decoded from its
    /// lossless-compressed fixture and run through the real, unmodified
    /// <see cref="AccurateRipChecksum.Compute"/> -- not a hardcoded literal
    /// asserted with no independent basis. The clip spans the track-1
    /// leading trim boundary (2939 samples, ~0.067s in), so this exercises
    /// the exact asymmetric-trim logic
    /// <see cref="AccurateRipChecksum.Compute"/>'s doc comment now records.
    /// The expected values were independently re-derived from the same real
    /// PCM by a separate Python port of the algorithm (not copy-pasted from
    /// this method's own output) -- see
    /// <c>docs/backlog-completed/002-accuraterip-track-1-trim-asymmetry.md</c>
    /// for that derivation. Treating a 15-second clip as if it were the
    /// disc's actual track 1 only makes sense for exercising the leading
    /// trim; it cannot be checked against the real AccurateRip database
    /// (that requires a full-track checksum) -- <see cref="AccurateRipClientTests.MatchAsync_RealAccurateRipFixture_MatchesGenuineDatabaseEntry"/>
    /// covers the live-database side separately, from the full real track.
    /// </summary>
    [Fact]
    public void Compute_RealTrack1Clip_MatchesIndependentlyComputedChecksum()
    {
        var flacPath = Path.Combine(AppContext.BaseDirectory, "AccurateRip", "Fixtures", "track1-clip15s.flac");
        var wavPath = Path.Combine(Path.GetTempPath(), $"whatinator-test-{Guid.NewGuid():N}.wav");
        try
        {
            DecodeFlac(flacPath, wavPath);
            var pcm = WavFile.ReadDataChunk(wavPath);

            var (v1, v2) = AccurateRipChecksum.Compute(pcm, trackNumber: 1, totalTracks: 9);

            Assert.Equal(0x046cc9afu, v1);
            Assert.Equal(0xf3154e63u, v2);
        }
        finally
        {
            if (File.Exists(wavPath))
            {
                File.Delete(wavPath);
            }
        }
    }

    /// <summary>Decodes a FLAC file to WAV via the real <c>flac</c> CLI, same pattern as <c>FlacEncoderTests</c>' <c>ffprobe</c> helper.</summary>
    /// <param name="flacPath">The source FLAC file.</param>
    /// <param name="wavPath">The destination WAV path.</param>
    private static void DecodeFlac(string flacPath, string wavPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "flac",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(wavPath);
        startInfo.ArgumentList.Add(flacPath);

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    /// <summary>
    /// Generates deterministic 32-bit sample values via a fixed
    /// multiplicative formula (not real audio) -- reproducible independently
    /// in any language.
    /// </summary>
    /// <param name="sampleCount">The number of 4-byte L+R sample pairs to generate.</param>
    private static byte[] GenerateSamples(int sampleCount)
    {
        var bytes = new byte[sampleCount * 4];
        for (var i = 0; i < sampleCount; i++)
        {
            var value = unchecked((uint)((ulong)(i + 1) * 2654435761UL));
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4, 4), value);
        }

        return bytes;
    }
}
