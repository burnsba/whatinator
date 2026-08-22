using Whatinator.Core.Metadata;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Rip;

/// <summary>Options controlling a <see cref="WhatinatorRipRunner.RipAsync"/> invocation.</summary>
/// <param name="Device">The optical drive device path to rip from, e.g. <c>/dev/sr1</c>.</param>
/// <param name="ReleaseInfo">The resolved release metadata -- supplies track titles/artists for tagging and file naming.</param>
/// <param name="Toc">The disc's frame-accurate table of contents (see <see cref="Whatinator.Core.Toc.CdrdaoTocReader"/>), read once by the caller before ripping.</param>
/// <param name="OutputDirectory">
/// The directory <see cref="WhatinatorRipRunner"/> writes FLAC (and, if
/// <see cref="KeepWav"/> is set, WAV) files into directly -- no external
/// subprocess working-directory split is needed, since this project writes
/// every file itself.
/// </param>
/// <param name="DiscNumber">
/// The 1-based disc number being ripped. Required (and validated against
/// <see cref="Metadata.ReleaseInfo.Media"/>'s count) when the release has
/// more than one disc; ignored (defaults to 1) for single-disc releases --
/// same convention as <see cref="Flac.FlacPackageOptions"/>/
/// <see cref="Mp3.Mp3PackageOptions"/>.
/// </param>
/// <param name="Offset">
/// The drive's sample read offset, passed to <see cref="CdParanoiaTrackOptions.Offset"/>
/// for every track. Defaults to <c>0</c> -- see
/// <see cref="WhatinatorConfig.GetReadOffset"/> for where callers typically
/// source this from.
/// </param>
/// <param name="Overread">Whether to pass <c>--force-overread</c> to every track read.</param>
/// <param name="KeepWav">
/// When <see langword="true"/>, each track's accepted WAV is left in
/// <see cref="OutputDirectory"/> alongside its <c>.flac</c> instead of being
/// deleted once that track's FLAC encode+verify succeeds. Default
/// <see langword="false"/> matches this project's existing "no WAV
/// survives" behavior.
/// </param>
/// <param name="MaxRetries">The maximum number of test+copy cd-paranoia cycles per track before giving up on it, forwarded to <see cref="CdParanoiaTrackOptions.MaxRetries"/>.</param>
public sealed record WhatinatorRipOptions(
    string Device,
    ReleaseInfo ReleaseInfo,
    DiscToc Toc,
    string OutputDirectory,
    int? DiscNumber = null,
    int Offset = 0,
    bool Overread = false,
    bool KeepWav = false,
    int MaxRetries = 5);
