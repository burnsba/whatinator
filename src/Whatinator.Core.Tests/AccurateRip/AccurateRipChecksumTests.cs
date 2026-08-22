using System.Buffers.Binary;
using Whatinator.Core.AccurateRip;

namespace Whatinator.Core.Tests;

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
