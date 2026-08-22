namespace Whatinator.Core.Metadata;

/// <summary>One disc ("medium", in MusicBrainz terms) of a release.</summary>
/// <param name="Position">The disc number within the release (1 for a single-disc release).</param>
/// <param name="Subtitle">
/// The disc's own title, if MusicBrainz has one (e.g. a 2-disc release
/// where disc 1 is "Venus Orbiting" and disc 2 is "Venus Live, Still
/// Orbiting") -- <see langword="null"/> or empty if the medium has no
/// title of its own, which is the common case for single-disc releases.
/// </param>
/// <param name="Tracks">Every track on this disc, in order.</param>
public sealed record MediumInfo(int Position, string? Subtitle, IReadOnlyList<TrackInfo> Tracks);
