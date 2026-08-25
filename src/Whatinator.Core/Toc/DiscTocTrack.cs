namespace Whatinator.Core.Toc;

/// <summary>One track's frame-accurate extent within a <see cref="DiscToc"/>.</summary>
/// <param name="TrackNumber">The track number, 1-based.</param>
/// <param name="StartFrame">The absolute CD frame (1/75th of a second) where the track's index 1 begins.</param>
/// <param name="EndFrame">
/// The absolute CD frame where the track ends, inclusive (i.e. the frame
/// immediately before the next track's <see cref="StartFrame"/>, or before
/// the leadout for the disc's last track) -- matches the same track-end
/// convention this port's algorithm research was cross-checked against
/// (see root <c>CLAUDE.md</c> § Gotchas).
/// </param>
/// <param name="IsAudio">Whether this is a playable audio track, as opposed to a data track.</param>
/// <param name="PregapFrames">
/// The length, in frames, of this track's pregap (the span between index 0
/// and index 1 -- i.e. <see cref="StartFrame"/> minus this value is where the
/// track's index 0 begins). <see langword="null"/> when either not scanned
/// (see <see cref="PregapScanned"/>) or scanned and found to genuinely be
/// zero -- use <see cref="PregapScanned"/> to tell those two cases apart,
/// this field alone can't.
/// </param>
/// <param name="Isrc">
/// This track's ISRC code, if the disc's TOC carries one -- <see langword="null"/> otherwise.
/// </param>
/// <param name="PregapScanned">
/// Whether <see cref="PregapFrames"/> reflects a real scan of this track
/// rather than an unscanned gap. Always <see langword="true"/> for track 1
/// (its pregap is read directly from the disc's raw TOC, no audio scan
/// needed) and for every track when <see cref="Toc.CdrdaoTocReader"/> was run
/// with <c>fastToc: false</c> (a full scan). <see langword="false"/> for
/// track 2 onward under <c>fastToc: true</c> -- fast mode skips the
/// audio-content scan those tracks' pregaps require, so <c>null</c> there
/// means "not scanned".
/// </param>
public sealed record DiscTocTrack(int TrackNumber, int StartFrame, int EndFrame, bool IsAudio, int? PregapFrames = null, string? Isrc = null, bool PregapScanned = false);
