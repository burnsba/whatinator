namespace Whatinator.Core.AccurateRip;

/// <summary>The outcome of matching a disc's locally computed checksums against the AccurateRip database.</summary>
public sealed class AccurateRipMatchResult
{
    /// <summary>
    /// Whether the disc's computed AccurateRip disc IDs returned any entry
    /// from the database at all (a 404 or network failure -- "not fatal",
    /// per this project's best-effort HTTP client contract -- makes this
    /// <see langword="false"/>, same as a genuinely unsubmitted disc).
    /// </summary>
    required public bool Found { get; init; }

    /// <summary>One match outcome per audio track on the disc, in track order.</summary>
    required public IReadOnlyList<AccurateRipTrackMatch> Tracks { get; init; }
}
