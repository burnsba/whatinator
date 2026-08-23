using Whatinator.Core.MusicBrainz;

namespace Whatinator.Core.Metadata;

/// <summary>
/// Orchestrates a MusicBrainz metadata lookup: given a disc ID, resolves
/// zero/one/many release matches into a <see cref="MetadataLookupResult"/>.
/// Deliberately knows nothing about optical drives or libdiscid -- the
/// caller reads the disc (see <c>Whatinator.LibDiscId.DiscReader</c>) and
/// passes in the resulting disc ID, which keeps this class unit-testable
/// against a fake <see cref="IMusicBrainzClient"/> without real hardware.
/// Does no console I/O -- presenting an ambiguous-match picker to the user
/// is the caller's job.
/// </summary>
public sealed class MetadataService
{
    private readonly IMusicBrainzClient _musicBrainzClient;

    /// <summary>Initializes a new instance of the <see cref="MetadataService"/> class.</summary>
    /// <param name="musicBrainzClient">The MusicBrainz client to query.</param>
    public MetadataService(IMusicBrainzClient musicBrainzClient)
    {
        ArgumentNullException.ThrowIfNull(musicBrainzClient);
        _musicBrainzClient = musicBrainzClient;
    }

    /// <summary>Looks up a disc ID on MusicBrainz and resolves the result.</summary>
    /// <param name="discId">The MusicBrainz disc ID to look up.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>
    /// A <see cref="MetadataLookupResult"/> describing whether the disc
    /// wasn't matched, was matched exactly once (already fully resolved via
    /// a second call to fetch full track listings), or matched multiple
    /// releases (needs disambiguation via <see cref="ResolveAsync"/>).
    /// </returns>
    /// <exception cref="MusicBrainzException">The MusicBrainz lookup failed.</exception>
    public async Task<MetadataLookupResult> LookupByDiscIdAsync(string discId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discId);

        var candidates = await _musicBrainzClient.LookupByDiscIdAsync(discId, cancellationToken).ConfigureAwait(false);

        switch (candidates.Count)
        {
            case 0:
                return MetadataLookupResult.NotFound;
            case 1:
                var releaseInfo = await ResolveAsync(candidates[0].MusicBrainzReleaseId, cancellationToken).ConfigureAwait(false);
                return MetadataLookupResult.Found(releaseInfo);
            default:
                return MetadataLookupResult.Ambiguous(candidates);
        }
    }

    /// <summary>
    /// Fetches the full metadata for a chosen release -- the second lookup
    /// step, called automatically for a single match, or by the caller
    /// after prompting the user to disambiguate an
    /// <see cref="MetadataLookupStatus.Ambiguous"/> result.
    /// </summary>
    /// <param name="releaseId">The chosen MusicBrainz release MBID.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The full release metadata.</returns>
    /// <exception cref="MusicBrainzException">The MusicBrainz lookup failed.</exception>
    public Task<ReleaseInfo> ResolveAsync(string releaseId, CancellationToken cancellationToken = default) =>
        _musicBrainzClient.GetReleaseAsync(releaseId, cancellationToken);
}
