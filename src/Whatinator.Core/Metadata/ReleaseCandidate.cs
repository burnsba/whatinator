using Whatinator.Core.MusicBrainz;

namespace Whatinator.Core.Metadata;

/// <summary>
/// A candidate release returned by a MusicBrainz disc ID lookup, before the
/// full track listing is fetched via <see cref="MusicBrainzClient.GetReleaseAsync"/>.
/// </summary>
/// <param name="MusicBrainzReleaseId">The MusicBrainz release MBID.</param>
/// <param name="Artist">The release's primary artist credit.</param>
/// <param name="Title">The release (album) title.</param>
/// <param name="Date">The release date, or <see langword="null"/> if unknown.</param>
/// <param name="Country">The release country, or <see langword="null"/> if unknown.</param>
/// <param name="Barcode">The release barcode, or <see langword="null"/> if unknown.</param>
/// <param name="CatalogNumber">The catalog number, or <see langword="null"/> if unknown.</param>
public sealed record ReleaseCandidate(
    string MusicBrainzReleaseId,
    string Artist,
    string Title,
    string? Date,
    string? Country,
    string? Barcode,
    string? CatalogNumber);
