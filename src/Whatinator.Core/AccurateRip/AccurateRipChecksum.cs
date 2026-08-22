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
    /// <remarks>
    /// The leading trim (track 1) and trailing trim (last track) below
    /// exclude 2939 and 2940 sample pairs respectively -- <b>not</b> a
    /// symmetric 2940/2940, despite <see cref="TrimSamplePairs"/> being one
    /// value used both places. This asymmetry looks like an off-by-one bug
    /// (see <c>docs/backlog-completed/002-accuraterip-track-1-trim-asymmetry.md</c>
    /// for the full writeup) but is not one: it is required to match the
    /// real AccurateRip database's own convention. Verified live on
    /// 2026-08-22 against a real 9-track disc in <c>/dev/sr1</c> (AccurateRip
    /// disc IDs <c>001326da</c>/<c>008badbd</c>, CDDB ID <c>630d2d09</c>) --
    /// track 1's checksum, computed by this exact method from a real
    /// cd-paranoia rip at the drive's confirmed +6-sample offset, matched
    /// two separate real confidence-rated entries in the live database
    /// response for that disc, and <c>offset-find</c> independently
    /// confirmed all 8 non-last tracks against the same response. Do not
    /// "fix" this asymmetry without re-verifying against live data first --
    /// see root <c>CLAUDE.md</c> § Gotchas: Ported algorithms.
    /// </remarks>
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

        // Bounds on the running MulBy (1-based) position counter. Not
        // symmetric -- see the verified-asymmetry note on this method's
        // <remarks/>. Track 1 excludes 2939 leading samples (mulBy in
        // [1, TrimSamplePairs) is excluded, i.e. i in [0, TrimSamplePairs-2]);
        // the last track excludes 2940 trailing samples (i in
        // [sampleCount-TrimSamplePairs, sampleCount-1]).
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
