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
/// track's index 0 begins). <see langword="null"/> when
/// <see cref="Toc.CdrdaoTocReader"/> was run with <c>fastToc: true</c> and no
/// pregap was detected for this track -- fast mode only ever reports track
/// 1's pregap (read directly from the disc's raw TOC, not audio-scanned);
/// every other track's pregap requires the slow scan <c>--fast-toc</c>
/// skips. A full-mode read that finds no pregap for a track (e.g. it starts
/// exactly where the previous one ends) also reports <see langword="null"/>
/// here, same as "not detected" -- there's no way to distinguish "not
/// scanned" from "scanned, found zero" from this field alone.
/// </param>
/// <param name="Isrc">
/// This track's ISRC code, if the disc's TOC carries one -- <see langword="null"/> otherwise.
/// </param>
public sealed record DiscTocTrack(int TrackNumber, int StartFrame, int EndFrame, bool IsAudio, int? PregapFrames = null, string? Isrc = null);
