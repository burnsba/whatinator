using Whatinator.Core.Flac;
using Whatinator.Core.Metadata;

namespace Whatinator.Core.Mp3;

/// <summary>Options for <see cref="Mp3Packager.PackageAsync"/>.</summary>
/// <param name="ReleaseInfo">The resolved release metadata.</param>
/// <param name="SourceDirectory">
/// A <see cref="FlacPackager"/>-produced disc folder (a <c>cdN/</c> folder
/// for a multi-disc release, or the FLAC container folder itself for
/// single-disc) -- MP3 encoding always sources from FLAC, never a WAV
/// (see root <c>CLAUDE.md</c> § Gotchas).
/// </param>
/// <param name="DestinationParentDirectory">The directory the release's MP3 container folder is created under.</param>
/// <param name="DiscNumber">
/// The 1-based disc number <paramref name="SourceDirectory"/>'s FLAC files
/// belong to. Required (and validated against
/// <see cref="Whatinator.Core.Metadata.ReleaseInfo.Media"/>'s count) when the
/// release has more than one disc; ignored (defaults to 1) for
/// single-disc releases.
/// </param>
public sealed record Mp3PackageOptions(
    ReleaseInfo ReleaseInfo,
    string SourceDirectory,
    string DestinationParentDirectory,
    int? DiscNumber = null);
