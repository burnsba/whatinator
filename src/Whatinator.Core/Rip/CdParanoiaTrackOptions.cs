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
/// <param name="Overread">Whether to pass <c>--force-overread</c> (read into the lead-out).</param>
/// <param name="MaxRetries">
/// The maximum number of test+copy cycles to attempt before giving up on
/// this track.
/// </param>
public sealed record CdParanoiaTrackOptions(
    string Device,
    DiscToc Toc,
    int TrackNumber,
    string DestinationWavPath,
    int Offset = 0,
    bool Overread = false,
    int MaxRetries = 5);
