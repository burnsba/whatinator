using Whatinator.Core.Toc;

namespace Whatinator.Core.Rip;

/// <summary>
/// Decides which single track, if any, <c>--force-overread</c> can possibly
/// affect on a given disc. Overreading only ever matters for the one track
/// touching the physical edge of the disc that the configured read offset
/// shifts into -- the disc's last track for a positive offset (samples are
/// shifted later, so the tail end needs data past the nominal end of the
/// disc), the first for a negative one (samples are shifted earlier, so the
/// very start needs data before the nominal start) -- every other track
/// never touches a physical boundary, so <c>--force-overread</c> is a no-op
/// for it regardless. See root <c>CLAUDE.md</c> § Gotchas for why
/// <see cref="WhatinatorRipRunner.RipAsync"/> scopes <c>--overread</c> to
/// just this one track rather than passing it to every track as before.
/// </summary>
public static class OverreadPolicy
{
    /// <summary>
    /// Resolves the physical track number at the disc boundary <paramref name="offset"/>
    /// shifts into.
    /// </summary>
    /// <param name="toc">The disc's frame-accurate table of contents.</param>
    /// <param name="offset">The drive's sample read offset.</param>
    /// <returns>
    /// <paramref name="toc"/>'s last track's number if <paramref name="offset"/>
    /// is positive, its first track's number if negative, or <see langword="null"/>
    /// if <paramref name="offset"/> is <c>0</c> (no boundary is shifted, so no
    /// track is affected). This is a purely physical-track-number answer --
    /// it says nothing about whether that track is actually audio (and
    /// therefore ever ripped); callers comparing this against an
    /// audio-only track list get that check for free, since a data track's
    /// number simply never matches anything in such a list.
    /// </returns>
    public static int? ResolveBoundaryTrackNumber(DiscToc toc, int offset)
    {
        ArgumentNullException.ThrowIfNull(toc);

        return offset switch
        {
            0 => null,
            > 0 => toc.Tracks[^1].TrackNumber,
            < 0 => toc.Tracks[0].TrackNumber,
        };
    }
}
