namespace Whatinator.Core.Discogs;

/// <summary>
/// The Discogs operations <c>make-releaseinfo</c> depends on. Exists so
/// callers can be unit-tested against a fake implementation instead of the
/// real network-calling <see cref="DiscogsClient"/>.
/// </summary>
public interface IDiscogsClient
{
    /// <summary>Searches Discogs for releases matching a barcode.</summary>
    /// <param name="barcode">The barcode (UPC/EAN) to search for.</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>Every matching release Discogs returns, best guess first.</returns>
    Task<IReadOnlyList<DiscogsInfo>> SearchByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

    /// <summary>Fetches a single release directly by its Discogs release ID.</summary>
    /// <param name="releaseId">The Discogs release ID (the numeric part of its release URL).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The release's metadata.</returns>
    Task<DiscogsInfo> GetReleaseAsync(string releaseId, CancellationToken cancellationToken = default);
}
