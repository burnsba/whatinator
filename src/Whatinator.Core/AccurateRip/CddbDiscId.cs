using Whatinator.Core.Toc;

namespace Whatinator.Core.AccurateRip;

/// <summary>
/// Computes the CDDB (freedb) disc ID -- a separate, older algorithm from
/// the AccurateRip disc IDs, needed because it's embedded in the
/// AccurateRip lookup filename for cross-validation. A pure-C# port of
/// the well-documented public freedb CDDB1 algorithm, matching
/// <c>cd-discid</c>'s own output -- see root <c>CLAUDE.md</c> § Gotchas for
/// this port's research provenance (including a dead-code trap the source
/// it was researched against had, worth knowing before touching this file).
/// </summary>
public static class CddbDiscId
{
    /// <summary>The number of CD frames per second.</summary>
    private const int FramesPerSecond = 75;

    /// <summary>The standard 2-second CD lead-in, added to every track's start frame before summing.</summary>
    private const int LeadInFrames = 2 * FramesPerSecond;

    /// <summary>Computes a disc's CDDB disc ID.</summary>
    /// <param name="toc">
    /// The disc's table of contents. Unlike <see cref="AccurateRipDiscId"/>,
    /// data tracks count toward the CDDB disc ID the same as audio tracks.
    /// </param>
    /// <returns>The disc ID, formatted as a lowercase 8-hex-digit string.</returns>
    public static string Compute(DiscToc toc)
    {
        ArgumentNullException.ThrowIfNull(toc);

        var digitSum = 0;
        foreach (var track in toc.Tracks)
        {
            var offsetSeconds = (track.StartFrame + LeadInFrames) / FramesPerSecond;
            digitSum += DigitSum(offsetSeconds);
        }

        var startSeconds = toc.Tracks[0].StartFrame / FramesPerSecond;
        var leadoutSeconds = toc.LeadoutFrame / FramesPerSecond;
        var totalLength = leadoutSeconds - startSeconds;
        var trackCount = toc.Tracks.Count;

        var value = (uint)(((digitSum % 255) << 24) |
            ((totalLength & 0xFFFF) << 8) |
            (trackCount & 0xFF));
        return value.ToString("x8");
    }

    /// <summary>Returns the sum of a non-negative integer's decimal digits (single pass, not further reduced).</summary>
    /// <param name="value">The value to sum the digits of.</param>
    private static int DigitSum(int value)
    {
        var sum = 0;
        while (value > 0)
        {
            sum += value % 10;
            value /= 10;
        }

        return sum;
    }
}
