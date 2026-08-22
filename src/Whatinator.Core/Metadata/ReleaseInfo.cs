using Whatinator.Core.Discogs;

namespace Whatinator.Core.Metadata;

/// <summary>
/// Everything whatinator persists about a release, written to
/// <c>releaseinfo.json</c> by the <c>make-releaseinfo</c> command and consumed
/// by every later phase (ripping, tagging, <c>id.txt</c>/log generation).
/// </summary>
/// <param name="MusicBrainzReleaseId">The MusicBrainz release MBID.</param>
/// <param name="MusicBrainzUrl">The MusicBrainz web page URL for this release.</param>
/// <param name="Artist">The release's primary artist credit.</param>
/// <param name="Title">The release (album) title.</param>
/// <param name="Date">The release date, as reported by MusicBrainz (may be partial, e.g. just a year), or <see langword="null"/> if unknown.</param>
/// <param name="Country">The release country (ISO 3166-1 alpha-2), or <see langword="null"/> if unknown.</param>
/// <param name="Barcode">The release barcode, or <see langword="null"/> if unknown.</param>
/// <param name="Label">The label name, or <see langword="null"/> if unknown.</param>
/// <param name="CatalogNumber">The catalog number, or <see langword="null"/> if unknown.</param>
/// <param name="Media">Every disc in the release, in order.</param>
/// <param name="Discogs">
/// The matching Discogs release, if one was resolved during
/// <c>make-releaseinfo</c> (best-effort -- see <see cref="DiscogsClient"/>),
/// or <see langword="null"/> if none was found, none was selected, or the
/// lookup wasn't attempted (e.g. no barcode, or this file was loaded via
/// <c>--releaseinfo</c> rather than a fresh lookup).
/// </param>
public sealed record ReleaseInfo(
    string MusicBrainzReleaseId,
    string MusicBrainzUrl,
    string Artist,
    string Title,
    string? Date,
    string? Country,
    string? Barcode,
    string? Label,
    string? CatalogNumber,
    IReadOnlyList<MediumInfo> Media,
    DiscogsInfo? Discogs = null);
