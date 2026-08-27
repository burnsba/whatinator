using Whatinator.Core.Toc;

namespace Whatinator.Core.Rip;

/// <summary>Options for one <see cref="CdParanoiaTrackReader"/> track read.</summary>
/// <param name="Device">The optical drive device path to read from, e.g. <c>/dev/sr1</c>.</param>
/// <param name="Toc">
/// The disc's frame-accurate table of contents (see
/// <see cref="Whatinator.Core.Toc.CdrdaoTocReader"/>) -- used to resolve
/// <see cref="TrackNumber"/>'s start/end frames for both the cd-paranoia
/// span argument and the expected-file-size check.
/// </param>
/// <param name="TrackNumber">The 1-based track number to read.</param>
/// <param name="DestinationWavPath">
/// Where the accepted (matched) WAV file is written on success. The
/// directory it lives in is also where this reader's scratch test/copy
/// temp files are created.
/// </param>
/// <param name="Offset">
/// The drive's sample read offset, passed to cd-paranoia's own
/// <c>--sample-offset</c> flag. Defaults to <c>0</c>. A value over
/// <see cref="CdParanoiaTrackReader.MaxSafeOffsetSamples"/>
/// triggers a warning (known upstream cd-paranoia bug -- see root
/// <c>CLAUDE.md</c> § Gotchas) rather than being rejected.
/// </param>
/// <param name="Overread">
/// Whether to pass <c>--force-overread</c> (read into the lead-in/lead-out).
/// Callers should only ever set this <see langword="true"/> for the one
/// track that actually touches the disc's physical boundary the configured
/// read offset shifts into -- see <see cref="OverreadPolicy.ResolveBoundaryTrackNumber"/>,
/// which <see cref="WhatinatorRipRunner.RipAsync"/> uses to scope it. It's a
/// no-op for every other track regardless.
/// </param>
/// <param name="MaxRetries">
/// The maximum number of test+copy cycles to attempt before giving up on
/// this track.
/// </param>
/// <param name="Verify">
/// Whether to perform the test/copy double-read and CRC32 compare. When
/// <see langword="false"/> (single-pass/"fast" mode -- see
/// <c>docs/backlog-completed/050-eac-gap-extraction-mode-and-retry-control.md</c>),
/// only one read is performed per attempt; the size check is the only local
/// verification. AccurateRip verification is unaffected either way -- it's
/// an independent, whole-disc check performed by <see cref="WhatinatorRipRunner"/>
/// after every track has been read.
/// </param>
/// <param name="MaxSectorReads">
/// Passed to cd-paranoia's own <c>--never-skip</c> flag, capping how many
/// times cd-paranoia itself will retry a single bad sector before accepting
/// it and moving on, rather than the ~20 it defaults to when the flag is
/// omitted entirely. <c>0</c> means infinite (passes a bare
/// <c>--never-skip</c> with no argument) -- see root <c>CLAUDE.md</c> §
/// Gotchas' <c>--force-overread</c> hang entry for why relying on
/// cd-paranoia's own unflagged default wasn't enough to prevent a rip
/// running all night on one sector.
/// </param>
/// <param name="StallTimeoutSeconds">
/// How many seconds a single cd-paranoia invocation (one test read, or one
/// copy read) may go without reporting forward progress before
/// <see cref="CdParanoiaTrackReader"/> kills it and counts the attempt as
/// failed, letting the existing <see cref="MaxRetries"/> cycle (and
/// eventual <see cref="CdParanoiaTrackResult.Degraded"/> path) take over
/// instead of hanging indefinitely. <c>0</c> disables the timeout entirely.
/// Combines multiplicatively with <see cref="MaxRetries"/> and
/// <see cref="Verify"/> for one track's worst-case wall-clock time -- see
/// the CLI's <c>--stall-timeout</c>/<c>--retries</c> help text.
/// </param>
/// <param name="SkipOverreadOnStall">
/// Only meaningful when <see cref="Overread"/> is <see langword="true"/>: if
/// an overread attempt stalls (see <see cref="StallTimeoutSeconds"/>),
/// whether <see cref="CdParanoiaTrackReader.ReadTrackAsync"/> should stop
/// retrying with overread on (confirmed, via direct reproduction against
/// real hardware, that it will just stall again -- see root <c>CLAUDE.md</c>
/// § Gotchas) and retry the track's remaining attempts with overread off
/// instead, accepting a silence-filled boundary. When <see langword="false"/>
/// (the default), a stalled overread attempt gives up on the track
/// immediately rather than exhausting <see cref="MaxRetries"/> retrying
/// something already known to fail the same way every time.
/// </param>
public sealed record CdParanoiaTrackOptions(
    string Device,
    DiscToc Toc,
    int TrackNumber,
    string DestinationWavPath,
    int Offset = 0,
    bool Overread = false,
    int MaxRetries = 5,
    bool Verify = true,
    int MaxSectorReads = 12,
    int StallTimeoutSeconds = 120,
    bool SkipOverreadOnStall = false);
