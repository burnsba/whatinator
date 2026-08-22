using Whatinator.Core.Metadata;
using Whatinator.Core.Rip;

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
public sealed record FlacPackageOptions(
    ReleaseInfo ReleaseInfo,
    string SourceDirectory,
    string DestinationParentDirectory,
    int? DiscNumber = null);
