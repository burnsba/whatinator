using Whatinator.Core.Toc;

namespace Whatinator.Core.AccurateRip;

/// <summary>
/// Computes the pair of AccurateRip disc IDs used to look up a disc in the
/// AccurateRip database -- pure frame-offset arithmetic, nothing looked up
/// from an external tool. See root <c>CLAUDE.md</c> § Gotchas for this
/// port's research provenance.
/// </summary>
public static class AccurateRipDiscId
{
    /// <summary>Computes a disc's two AccurateRip disc IDs.</summary>
    /// <param name="toc">The disc's table of contents.</param>
    /// <returns>Both disc IDs, formatted as lowercase 8-hex-digit strings.</returns>
    /// <remarks>
    /// Data tracks are excluded from the running sum, but the last track on
    /// the disc -- audio or data -- still determines where the leadout offset
    /// lands, matching this exact behavior as documented in the algorithm's
    /// original reference source (see root <c>CLAUDE.md</c> § Gotchas).
    /// </remarks>
    public static (string DiscId1, string DiscId2) Compute(DiscToc toc)
    {
        ArgumentNullException.ThrowIfNull(toc);

        uint discId1 = 0;
        uint discId2 = 0;
        var audioTrackCount = 0;

        foreach (var track in toc.Tracks)
        {
            if (!track.IsAudio)
            {
                continue;
            }

            audioTrackCount++;
            var offset = (uint)track.StartFrame;
            discId1 += offset;
            discId2 += (offset == 0 ? 1 : offset) * (uint)track.TrackNumber;
        }

        var leadout = (uint)toc.LeadoutFrame;
        discId1 += leadout;
        discId2 += leadout * (uint)(audioTrackCount + 1);

        return (discId1.ToString("x8"), discId2.ToString("x8"));
    }
}
