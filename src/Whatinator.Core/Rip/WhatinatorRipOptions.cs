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
/// <param name="Overread">
/// Whether <c>--force-overread</c> should be used at all for this rip.
/// <see cref="WhatinatorRipRunner.RipAsync"/> scopes it to the single track
/// that actually touches the disc boundary <see cref="Offset"/> shifts into
/// (see <see cref="OverreadPolicy.ResolveBoundaryTrackNumber"/>) -- it's a
/// no-op on every other track, so this option no longer means "every track."
/// </param>
/// <param name="KeepWav">
/// When <see langword="true"/>, each track's accepted WAV is left in
/// <see cref="OutputDirectory"/> alongside its <c>.flac</c> instead of being
/// deleted once that track's FLAC encode+verify succeeds. Default
/// <see langword="false"/> matches this project's existing "no WAV
/// survives" behavior.
/// </param>
/// <param name="MaxRetries">The maximum number of test+copy cd-paranoia cycles per track before giving up on it, forwarded to <see cref="CdParanoiaTrackOptions.MaxRetries"/>.</param>
/// <param name="Verify">Whether to perform the test/copy double-read and CRC32 compare on every track, forwarded to <see cref="CdParanoiaTrackOptions.Verify"/>.</param>
/// <param name="MaxSectorReads">The per-sector retry cap passed to cd-paranoia's <c>--never-skip</c>, forwarded to <see cref="CdParanoiaTrackOptions.MaxSectorReads"/>.</param>
/// <param name="StallTimeoutSeconds">How many seconds a stalled cd-paranoia invocation is allowed before it's killed and counted as a failed attempt, forwarded to <see cref="CdParanoiaTrackOptions.StallTimeoutSeconds"/>.</param>
/// <param name="SkipOverreadOnStall">Whether a stalled overread attempt should retry with overread off instead of giving up on the track, forwarded to <see cref="CdParanoiaTrackOptions.SkipOverreadOnStall"/>.</param>
public sealed record WhatinatorRipOptions(
    string Device,
    ReleaseInfo ReleaseInfo,
    DiscToc Toc,
    string OutputDirectory,
    int? DiscNumber = null,
    int Offset = 0,
    bool Overread = false,
    bool KeepWav = false,
    int MaxRetries = 5,
    bool Verify = true,
    int MaxSectorReads = 12,
    int StallTimeoutSeconds = 120,
    bool SkipOverreadOnStall = false);
