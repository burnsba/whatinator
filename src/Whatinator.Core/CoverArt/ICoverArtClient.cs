using Whatinator.Core.Flac;

namespace Whatinator.Core.CoverArt;

/// <summary>
/// The cover art operation <see cref="FlacPackager"/> depends on. Exists so
/// callers can be unit-tested against a fake implementation instead of the
/// real network-calling <see cref="CoverArtClient"/>.
/// </summary>
public interface ICoverArtClient
{
    /// <summary>Attempts to download a release's front cover image.</summary>
    /// <param name="musicBrainzReleaseId">The MusicBrainz release MBID.</param>
    /// <returns>The downloaded image, or <see langword="null"/> if none is available or the request failed.</returns>
    Task<CoverArtResult?> TryDownloadFrontCoverAsync(string musicBrainzReleaseId);
}
