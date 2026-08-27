namespace Whatinator.Core.Rip;

/// <summary>The outcome of a <see cref="WhatinatorRipRunner.RipAsync"/> invocation.</summary>
/// <param name="Tracks">Every audio track's outcome, in track order.</param>
/// <param name="AccurateRipFound">
/// Whether the disc's computed AccurateRip disc IDs returned any database
/// entry at all. Always <see langword="false"/> when <see cref="Degraded"/>
/// is <see langword="true"/> -- the whole-disc AccurateRip lookup needs a
/// computed checksum for every audio track (see
/// <see cref="AccurateRip.AccurateRipClient.MatchAsync"/>'s own count
/// check), so a rip that had to skip a track never attempts it. Not fatal
/// either way, same best-effort contract as the rest of this project's
/// AccurateRip integration.
/// </param>
/// <param name="SkippedDataTrackCount">How many data tracks were present on the disc and skipped (never ripped).</param>
/// <param name="OverreadTrackNumber">
/// The track number <c>--force-overread</c> was actually applied to, or
/// <see langword="null"/> if <see cref="WhatinatorRipOptions.Overread"/> was
/// <see langword="false"/>, or it was <see langword="true"/> but had no
/// effect (a zero read offset, or the disc's boundary track being a data
/// track never ripped) -- see <see cref="OverreadPolicy.ResolveBoundaryTrackNumber"/>.
/// Carried on the result (rather than recomputed by the caller) so
/// <see cref="Rip.EacLogOptions"/> can render the same decision that was
/// printed live during the rip.
/// </param>
public sealed record WhatinatorRipResult(
    IReadOnlyList<WhatinatorTrackRipResult> Tracks,
    bool AccurateRipFound,
    int SkippedDataTrackCount,
    int? OverreadTrackNumber = null)
{
    /// <summary>Whether every audio track was read and encoded successfully.</summary>
    public bool Success => Tracks.Count > 0 && Tracks.All(t => !t.Degraded);

    /// <summary>
    /// Whether one or more tracks had to be skipped after exhausting their
    /// own retries -- not a failure: whatever tracks *were* ripped are still
    /// valid and worth packaging, per <c>init.md</c>'s "allow bad data
    /// capture just to get through capturing the cd".
    /// </summary>
    public bool Degraded => Tracks.Any(t => t.Degraded);
}
