using Whatinator.Core.Toc;

namespace Whatinator.Core.AccurateRip;

/// <summary>
/// The raw AccurateRip database entry lookup <see cref="Drive.OffsetFinder"/>
/// depends on -- kept separate from <see cref="IAccurateRipClient"/> because
/// offset-finding needs to inspect a disc's raw per-entry, per-track
/// checksum data directly (testing one track's checksum against every
/// entry at a time, before the rest of the disc has been read) rather than
/// <see cref="IAccurateRipClient.MatchAsync"/>'s all-tracks-computed-up-front
/// contract. <see cref="AccurateRipClient"/> implements both interfaces.
/// </summary>
public interface IAccurateRipEntryLookup
{
    /// <summary>Fetches a disc's raw AccurateRip database entries.</summary>
    /// <param name="toc">The disc's table of contents.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>
    /// Every entry parsed from the database response, or an empty list if
    /// the disc has no database entry at all (404, network failure, or a
    /// genuinely empty response) -- never throws, same best-effort contract
    /// as <see cref="IAccurateRipClient.MatchAsync"/>.
    /// </returns>
    Task<IReadOnlyList<AccurateRipDbEntry>> GetEntriesAsync(DiscToc toc, CancellationToken cancellationToken = default);
}
