using Whatinator.Core.Toc;

namespace Whatinator.Core.AccurateRip;

/// <summary>
/// The AccurateRip database lookup/match operation phase 016's rip logger
/// depends on. Exists so callers can be unit-tested against a fake
/// implementation instead of the real network-calling
/// <see cref="AccurateRipClient"/>.
/// </summary>
public interface IAccurateRipClient
{
    /// <summary>
    /// Looks up a disc in the AccurateRip database and matches its response
    /// against locally computed checksums.
    /// </summary>
    /// <param name="toc">The disc's table of contents.</param>
    /// <param name="computedChecksums">
    /// One (v1, v2) checksum pair per audio track on the disc, in track
    /// order -- as computed by <see cref="AccurateRipChecksum.Compute"/>.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>
    /// The match outcome. Never throws for a 404 or network failure -- those
    /// come back as <see cref="AccurateRipMatchResult.Found"/> being
    /// <see langword="false"/>, same as this project's other best-effort
    /// HTTP clients.
    /// </returns>
    Task<AccurateRipMatchResult> MatchAsync(
        DiscToc toc,
        IReadOnlyList<(uint V1, uint V2)> computedChecksums,
        CancellationToken cancellationToken = default);
}
