using Whatinator.Core.Metadata;

namespace Whatinator.Core.MusicBrainz;

/// <summary>
/// The MusicBrainz operations <see cref="MetadataService"/> depends on.
/// Exists so <see cref="MetadataService"/> can be unit-tested against a fake
/// implementation instead of the real network-calling
/// <see cref="MusicBrainzClient"/>.
/// </summary>
public interface IMusicBrainzClient
{
    /// <summary>Looks up every release matching the given MusicBrainz disc ID.</summary>
    /// <param name="discId">The MusicBrainz disc ID.</param>
    /// <returns>Every matching release, without full track listings.</returns>
    Task<IReadOnlyList<ReleaseCandidate>> LookupByDiscIdAsync(string discId);

    /// <summary>Fetches the full metadata (including every disc's full track listing) for a release.</summary>
    /// <param name="releaseId">The MusicBrainz release MBID.</param>
    /// <returns>The full release metadata.</returns>
    Task<ReleaseInfo> GetReleaseAsync(string releaseId);
}
