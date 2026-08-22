using System.Buffers.Binary;

namespace Whatinator.Core.AccurateRip;

/// <summary>
/// Computes AccurateRip v1/v2 checksums for a track's raw PCM samples. A
/// pure-C# port of the public AccurateRip v1/v2 algorithm
/// (hydrogenaudio.org/forums/index.php?showtopic=97603) -- see root
/// <c>CLAUDE.md</c> § Gotchas for this port's research provenance.
/// </summary>
public static class AccurateRipChecksum
{
    /// <summary>The number of bytes in one CD sector's worth of audio.</summary>
    private const int SectorBytes = 2352;

    /// <summary>
    /// The number of leading/trailing L+R sample pairs (5 sectors' worth)
    /// excluded from the first/last track's checksum.
    /// </summary>
    private const int TrimSamplePairs = (SectorBytes * 5) / 4;

    /// <summary>Computes a track's AccurateRip v1 and v2 checksums.</summary>
    /// <param name="pcmData">
    /// The track's raw 16-bit stereo PCM samples -- a WAV file's <c>data</c>
    /// chunk with no header, as returned by <see cref="WavFile.ReadDataChunk"/>.
    /// </param>
    /// <param name="trackNumber">The track's 1-based position on the disc.</param>
    /// <param name="totalTracks">The disc's total track count.</param>
    /// <returns>The computed v1 and v2 checksums.</returns>
    public static (uint V1, uint V2) Compute(byte[] pcmData, int trackNumber, int totalTracks)
    {
        ArgumentNullException.ThrowIfNull(pcmData);

        var sampleCount = pcmData.Length / 4;

        // 1-based inclusive bounds on the running MulBy position counter.
        var from = 0;
        var to = sampleCount;
        if (trackNumber == 1)
        {
            from += TrimSamplePairs;
        }

        if (trackNumber == totalTracks)
        {
            to -= TrimSamplePairs;
        }

        uint csumHi = 0;
        uint csumLo = 0;
        for (var i = 0; i < sampleCount; i++)
        {
            var mulBy = i + 1;
            if (mulBy >= from && mulBy <= to)
            {
                var sample = BinaryPrimitives.ReadUInt32LittleEndian(pcmData.AsSpan(i * 4, 4));
                var product = (ulong)sample * (ulong)mulBy;
                csumHi += (uint)(product >> 32);
                csumLo += (uint)product;
            }
        }

        var v1 = csumLo;
        var v2 = csumLo + csumHi;
        return (v1, v2);
    }
}
