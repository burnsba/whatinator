using Whatinator.Core.Drive;
using Whatinator.Core.Metadata;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Rip;

/// <summary>Everything <see cref="WhatinatorEacLog.Format"/> needs to render one disc's rip log.</summary>
/// <param name="ReleaseInfo">The resolved release metadata -- supplies the header's <c>{Artist} / {Title}</c> line.</param>
/// <param name="RipResult">The completed rip's per-track outcomes and whole-disc AccurateRip result.</param>
/// <param name="Toc">The disc's frame-accurate table of contents, for the TOC section and each track's pregap/length.</param>
/// <param name="DiscDirectory">
/// The final directory this disc's files live in (i.e. after
/// <see cref="Flac.FlacPackager"/> has moved them) -- combined with each
/// track's file name to build the per-track <c>Filename</c> line.
/// </param>
/// <param name="DevicePath">The block device the disc was ripped from, e.g. <c>/dev/sr1</c>.</param>
/// <param name="DriveVendor">The drive's vendor string, or <see langword="null"/> if unknown.</param>
/// <param name="DriveModel">The drive's model string, or <see langword="null"/> if unknown.</param>
/// <param name="DriveRelease">The drive's firmware revision, or <see langword="null"/> if unknown.</param>
/// <param name="ReadOffset">The sample read offset the rip used.</param>
/// <param name="Overread">Whether <c>--overread</c> was given for the rip (not whether it had any effect -- see <see cref="OverreadTrackNumber"/>).</param>
/// <param name="CacheDefeat">The drive's <see cref="CacheDefeatAnalyzer"/> result, or <see cref="CacheDefeatResult.Unknown"/> if never analyzed.</param>
/// <param name="CdParanoiaVersion">The <c>cd-paranoia --version</c> banner, from <see cref="SystemInfo.GetCdParanoiaVersion"/>.</param>
/// <param name="CdrdaoVersion">The <c>cdrdao</c> version banner, from <see cref="SystemInfo.GetCdrdaoVersion"/>.</param>
/// <param name="FlacVersion">The <c>flac --version</c> output, from <see cref="SystemInfo.GetFlacVersion"/>.</param>
/// <param name="Uname">The <c>uname -a</c> output, from <see cref="SystemInfo.GetUname"/>.</param>
/// <param name="OsPrettyName">The OS's <c>PRETTY_NAME</c>, from <see cref="SystemInfo.GetOsPrettyName"/>.</param>
/// <param name="StartTime">When the rip started -- also the log's own dated header line.</param>
/// <param name="EndTime">When the rip finished.</param>
/// <param name="Verify">
/// Whether the rip performed the test/copy double-read and CRC32 compare
/// (matches <see cref="WhatinatorRipOptions.Verify"/>). Governs the header's
/// <c>Read mode</c> line (<c>Secure</c> vs. EAC's own <c>Burst</c> term for
/// single-pass) and whether each track's <c>Test CRC</c> line reports a real
/// value or that verification was skipped -- see
/// <c>docs/backlog-completed/050-eac-gap-extraction-mode-and-retry-control.md</c>.
/// Default <see langword="true"/> matches this project's prior always-Secure
/// behavior.
/// </param>
/// <param name="OverreadTrackNumber">
/// The track <c>--force-overread</c> actually applied to, from
/// <see cref="WhatinatorRipResult.OverreadTrackNumber"/> -- <see langword="null"/>
/// when <see cref="Overread"/> is <see langword="false"/>, or was
/// <see langword="true"/> but had no effect. Governs the "Overread into
/// Lead-In and Lead-Out" settings line.
/// </param>
public sealed record EacLogOptions(
    ReleaseInfo ReleaseInfo,
    WhatinatorRipResult RipResult,
    DiscToc Toc,
    string DiscDirectory,
    string DevicePath,
    string? DriveVendor,
    string? DriveModel,
    string? DriveRelease,
    int ReadOffset,
    bool Overread,
    CacheDefeatResult CacheDefeat,
    string CdParanoiaVersion,
    string CdrdaoVersion,
    string FlacVersion,
    string Uname,
    string? OsPrettyName,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool Verify = true,
    int? OverreadTrackNumber = null);
