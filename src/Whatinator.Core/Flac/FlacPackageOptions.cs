using Whatinator.Core.Metadata;
using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Flac;

/// <summary>Options for <see cref="FlacPackager.PackageAsync"/>.</summary>
/// <param name="ReleaseInfo">The resolved release metadata.</param>
/// <param name="SourceDirectory">The directory this disc was ripped into (see <see cref="WhatinatorRipOptions.OutputDirectory"/>).</param>
/// <param name="DestinationParentDirectory">The directory the release's container folder is created under.</param>
/// <param name="DiscNumber">
/// The 1-based disc number this source directory's rip corresponds to.
/// Required (and validated against <see cref="Whatinator.Core.Metadata.ReleaseInfo.Media"/>'s
/// count) when the release has more than one disc; ignored (defaults to 1)
/// for single-disc releases.
/// </param>
/// <param name="DiscCatalogNumber">
/// This disc's UPC/EAN catalogue number, from <see cref="Toc.DiscToc.CatalogNumber"/>,
/// or <see langword="null"/> if unknown -- written into <c>id.txt</c>'s
/// <c>upc:</c> line. Named distinctly from <see cref="Whatinator.Core.Metadata.ReleaseInfo.CatalogNumber"/>,
/// which is unrelated MusicBrainz/Discogs label data.
/// </param>
/// <param name="Toc">
/// This disc's physical table of contents, used to write the <c>.cue</c>
/// sheet's <c>CATALOG</c>/<c>ISRC</c>/pregap data (see
/// <see cref="Whatinator.Core.CueSheetFile"/>) -- <see langword="null"/> if
/// unavailable, in which case the cue sheet is still written but without
/// that data. Threaded per call rather than expected on
/// <see cref="Whatinator.Core.Metadata.ReleaseInfo"/> for the same reason as
/// <paramref name="DiscCatalogNumber"/> above.
/// </param>
public sealed record FlacPackageOptions(
    ReleaseInfo ReleaseInfo,
    string SourceDirectory,
    string DestinationParentDirectory,
    int? DiscNumber = null,
    string? DiscCatalogNumber = null,
    DiscToc? Toc = null);
