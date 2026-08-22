namespace Whatinator.Core.Drive;

/// <summary>Why <see cref="OffsetFinder.FindAsync"/> didn't find an offset.</summary>
public enum OffsetFindFailureReason
{
    /// <summary>
    /// The disc has fewer than 3 audio tracks -- the algorithm needs at
    /// least that many to work.
    /// </summary>
    TooFewTracks,

    /// <summary>
    /// The disc has no entry in the AccurateRip database at all --
    /// offset-finding needs a disc that's actually in the database to
    /// compare candidate reads against.
    /// </summary>
    NoAccurateRipEntry,

    /// <summary>
    /// Every candidate offset was tried and none produced a full match.
    /// Never falls back to a partial-match "best guess" -- a wrong offset
    /// silently accepted is worse than an honest failure here.
    /// </summary>
    NoOffsetMatched,
}
