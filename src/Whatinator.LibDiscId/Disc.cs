namespace Whatinator.LibDiscId;

/// <summary>The result of reading a disc's TOC via <see cref="DiscReader.Read"/>.</summary>
/// <param name="Id">The MusicBrainz disc ID.</param>
/// <param name="FreedbId">The FreeDB disc ID (without category prefix).</param>
/// <param name="SubmissionUrl">The MusicBrainz URL for submitting this TOC as a new disc ID.</param>
/// <param name="TocString">The raw TOC string (first track, last track, leadout, then per-track offsets).</param>
/// <param name="FirstTrack">The number of the first audio track.</param>
/// <param name="LastTrack">The number of the last audio track.</param>
/// <param name="Sectors">The total sector count (the leadout track's offset).</param>
/// <param name="Tracks">Every audio track's position and length, in order.</param>
public sealed record Disc(
    string Id,
    string FreedbId,
    string SubmissionUrl,
    string TocString,
    int FirstTrack,
    int LastTrack,
    int Sectors,
    IReadOnlyList<Track> Tracks);
