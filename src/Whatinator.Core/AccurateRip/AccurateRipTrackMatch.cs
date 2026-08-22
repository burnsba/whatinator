namespace Whatinator.Core.AccurateRip;

/// <summary>
/// One track's AccurateRip verification outcome -- shaped to carry exactly
/// what phase 016's rip logger (<see cref="Whatinator.Core.Rip.WhatinatorEacLog"/>) needs.
/// </summary>
public sealed class AccurateRipTrackMatch
{
    /// <summary>The track's 1-based position on the disc.</summary>
    required public int TrackNumber { get; init; }

    /// <summary>The locally computed AccurateRip v1 checksum.</summary>
    required public uint ComputedV1 { get; init; }

    /// <summary>The locally computed AccurateRip v2 checksum.</summary>
    required public uint ComputedV2 { get; init; }

    /// <summary>
    /// The database's v1 CRC, if <see cref="ComputedV1"/> matched some
    /// database entry for this track (formatted as an 8-hex-digit string);
    /// <see langword="null"/> if no v1 entry matched.
    /// </summary>
    public string? MatchedCrcV1 { get; init; }

    /// <summary>The confidence of the matched v1 entry, or <see langword="null"/> if <see cref="MatchedCrcV1"/> is <see langword="null"/>.</summary>
    public byte? ConfidenceV1 { get; init; }

    /// <summary>
    /// The database's v2 CRC, if <see cref="ComputedV2"/> matched some
    /// database entry for this track (formatted as an 8-hex-digit string);
    /// <see langword="null"/> if no v2 entry matched.
    /// </summary>
    public string? MatchedCrcV2 { get; init; }

    /// <summary>The confidence of the matched v2 entry, or <see langword="null"/> if <see cref="MatchedCrcV2"/> is <see langword="null"/>.</summary>
    public byte? ConfidenceV2 { get; init; }

    /// <summary>
    /// The highest confidence recorded for this track across every database
    /// entry, regardless of whether it matched either computed checksum;
    /// <see langword="null"/> if the disc had no database entries at all.
    /// </summary>
    public byte? MaxConfidence { get; init; }

    /// <summary>The CRC associated with <see cref="MaxConfidence"/>, formatted as an 8-hex-digit string.</summary>
    public string? MaxConfidenceCrc { get; init; }

    /// <summary>Whether this track matched the database on either AccurateRip version.</summary>
    public bool IsMatch => MatchedCrcV1 is not null || MatchedCrcV2 is not null;
}
