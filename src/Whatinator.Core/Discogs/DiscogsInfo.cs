using Whatinator.Core.Metadata;

namespace Whatinator.Core.Discogs;

/// <summary>
/// A Discogs release, either as a search candidate or (once chosen) the
/// value persisted on <see cref="ReleaseInfo.Discogs"/>.
/// </summary>
/// <param name="Id">The Discogs release ID.</param>
/// <param name="Title">The Discogs listing title (typically "Artist - Album").</param>
/// <param name="Country">The release country, or <see langword="null"/> if unknown.</param>
/// <param name="Format">A display-ready format string (e.g. <c>"CD, Album"</c>), or <see langword="null"/> if unknown.</param>
/// <param name="Genre">A display-ready, comma-joined genre list, or <see langword="null"/> if unknown.</param>
/// <param name="Style">A display-ready, comma-joined style list, or <see langword="null"/> if unknown.</param>
/// <param name="Label">The primary label name, or <see langword="null"/> if unknown.</param>
/// <param name="CatalogNumber">The catalog number, or <see langword="null"/> if unknown.</param>
/// <param name="Url">The Discogs web page URL for this release.</param>
public sealed record DiscogsInfo(
    string Id,
    string Title,
    string? Country,
    string? Format,
    string? Genre,
    string? Style,
    string? Label,
    string? CatalogNumber,
    string Url);
