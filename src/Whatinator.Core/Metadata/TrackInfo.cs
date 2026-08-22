namespace Whatinator.Core.Metadata;

/// <summary>One track's metadata as captured from MusicBrainz.</summary>
/// <param name="Number">The track number (1-based) within its medium.</param>
/// <param name="Title">The track title.</param>
/// <param name="Artist">The track's artist credit (may differ from the release artist, e.g. on compilations).</param>
/// <param name="Duration">The track's duration.</param>
public sealed record TrackInfo(int Number, string Title, string Artist, TimeSpan Duration);
