using Whatinator.Core.Flac;
using Whatinator.Core.Metadata;
using Whatinator.Core.Mp3;

namespace Whatinator.Core.Rip;

/// <summary>Options for one disc's <see cref="PipelineRunner.RunDiscAsync"/> call.</summary>
/// <param name="ReleaseInfo">The resolved release metadata.</param>
/// <param name="DiscNumber">
/// The 1-based disc number being ripped this call. Required (and validated
/// against <see cref="Whatinator.Core.Metadata.ReleaseInfo.Media"/>'s count) when
/// the release has more than one disc; ignored (defaults to 1) for
/// single-disc releases -- same convention as <see cref="FlacPackageOptions"/>/
/// <see cref="Mp3PackageOptions"/>.
/// </param>
/// <param name="Device">The optical drive device path to rip from.</param>
/// <param name="DestinationParentDirectory">The directory the release's container folder(s) are created under.</param>
/// <param name="SkipFlacPackaging">
/// When <see langword="true"/>, skips <see cref="FlacPackager"/> entirely --
/// the raw rip output is left on disk (never deleted; see
/// <see cref="PipelineDiscResult.RawRipDirectory"/>) rather than organized
/// into the standard FLAC container. MP3 encoding, if requested, sources
/// directly from that raw directory instead.
/// </param>
/// <param name="CreateMp3">Whether to run <see cref="Mp3Packager"/> for this disc.</param>
/// <param name="Offset">The drive's sample read offset, forwarded to <see cref="WhatinatorRipOptions.Offset"/>.</param>
/// <param name="Overread">Whether to pass <c>--force-overread</c> to every track read, forwarded to <see cref="WhatinatorRipOptions.Overread"/>.</param>
/// <param name="KeepWav">Whether to retain each track's accepted WAV, forwarded to <see cref="WhatinatorRipOptions.KeepWav"/>.</param>
/// <param name="Environment">
/// Drive/tool info to write an EAC-style rip log from (phase 016) -- see
/// <see cref="RipEnvironmentInfo"/>. <see langword="null"/> skips writing a
/// log entirely (no caller currently omits this outside tests).
/// </param>
/// <param name="FastToc">
/// Whether to pass <c>fastToc: true</c> to <see cref="Toc.CdrdaoTocReader.ReadAsync"/>.
/// Default <see langword="false"/> -- the TOC read scans every track's
/// pregap, at the cost of roughly a second per track or more. Set
/// <see langword="true"/> to restore the old fast read (track start/length
/// only, track 1's pregap only) when that cost isn't worth paying.
/// </param>
public sealed record PipelineDiscOptions(
    ReleaseInfo ReleaseInfo,
    int? DiscNumber,
    string Device,
    string DestinationParentDirectory,
    bool SkipFlacPackaging,
    bool CreateMp3,
    int? Offset = null,
    bool Overread = false,
    bool KeepWav = false,
    RipEnvironmentInfo? Environment = null,
    bool FastToc = false);
