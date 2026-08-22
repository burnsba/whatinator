using Whatinator.Core.Metadata;

namespace Whatinator.Core.Naming;

/// <summary>
/// Builds a ripped track's base file name (no extension) directly, replacing
/// the subprocess-template machinery this project's own rip path used to
/// need before phase 015's native rip cutover (see root <c>CLAUDE.md</c> §
/// Gotchas) -- <see cref="Whatinator.Core.Rip.WhatinatorRipRunner"/> writes
/// every file itself, so there's no external template/workaround left to
/// carry over, just the underlying naming decision.
/// </summary>
public static class TrackFileNaming
{
    /// <summary>
    /// Whether every track's artist, across every disc of
    /// <paramref name="releaseInfo"/>, matches the release artist -- the
    /// same whole-release decision this project's pre-phase-015 template
    /// resolver made, ported as a plain predicate.
    /// </summary>
    /// <param name="releaseInfo">The release being ripped.</param>
    /// <returns><see langword="false"/> when every track's artist matches the release artist (the common single-artist-album case); <see langword="true"/> otherwise (e.g. a various-artists compilation).</returns>
    public static bool UsesPerTrackArtist(ReleaseInfo releaseInfo)
    {
        ArgumentNullException.ThrowIfNull(releaseInfo);

        return !releaseInfo.Media
            .SelectMany(medium => medium.Tracks)
            .All(track => track.Artist == releaseInfo.Artist);
    }

    /// <summary>
    /// Builds a track's base file name (no extension): a zero-padded track
    /// number, a literal <c>" - "</c> (never a period --
    /// <see cref="Whatinator.Core.Rip.TrackFileMatcher"/> parses only the
    /// leading digits back out, so either form round-trips), then (for a
    /// various-artists release, see <see cref="UsesPerTrackArtist"/>) the
    /// track artist and another <c>" - "</c>, then the title -- sanitized for
    /// the filesystem.
    /// </summary>
    /// <param name="releaseInfo">The release being ripped.</param>
    /// <param name="track">The track to name.</param>
    /// <returns>The sanitized base file name.</returns>
    public static string BuildBaseFileName(ReleaseInfo releaseInfo, TrackInfo track)
    {
        ArgumentNullException.ThrowIfNull(releaseInfo);
        ArgumentNullException.ThrowIfNull(track);

        var name = UsesPerTrackArtist(releaseInfo)
            ? $"{track.Number:D2} - {track.Artist} - {track.Title}"
            : $"{track.Number:D2} - {track.Title}";

        return FileNameSanitizer.Sanitize(name);
    }
}
