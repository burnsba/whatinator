using System.Globalization;
using System.Text;
using Whatinator.Core.Metadata;
using Whatinator.Core.Toc;

namespace Whatinator.Core;

/// <summary>
/// Formats and writes one disc's <c>.cue</c> sheet -- one <c>FILE</c> entry
/// per ripped track (matching this project's file-per-track output), with
/// <c>CATALOG</c>/<c>ISRC</c>/pregap data from <see cref="Toc.DiscToc"/> when
/// known. Pure formatting -- no network or disc I/O. Deliberately never
/// generated for the MP3 folder (<see cref="Flac.FlacPackager"/> writes this,
/// <see cref="Mp3.Mp3Packager"/> does not) -- a cue sheet exists to make a
/// rip losslessly reconstructible to disc, which a lossy MP3 encode can never
/// serve, so there's nothing for one to verify there.
/// </summary>
public static class CueSheetFile
{
    /// <summary>CD frames per second, for <c>MM:SS:FF</c> index timecodes.</summary>
    private const int FramesPerSecond = 75;

    /// <summary>
    /// Formats <paramref name="tracks"/> as one disc's <c>.cue</c> sheet
    /// content.
    /// </summary>
    /// <param name="releaseInfo">The release this disc belongs to.</param>
    /// <param name="tracks">
    /// This disc's present tracks paired with their packaged audio file
    /// name (no directory component -- <c>FILE</c> lines are relative), in
    /// track-number order. Same shape <see cref="Rip.TrackFileMatcher.Match"/>
    /// returns: a degraded rip's missing tracks are simply absent, not
    /// padded with placeholders.
    /// </param>
    /// <param name="toc">
    /// This disc's physical table of contents, for <c>CATALOG</c>, per-track
    /// <c>ISRC</c>, and pregap placement -- or <see langword="null"/> if
    /// unavailable, in which case all three are omitted rather than guessed.
    /// </param>
    /// <returns>The formatted text, ready to write to a file.</returns>
    public static string Format(ReleaseInfo releaseInfo, IReadOnlyList<(TrackInfo Track, string FileName)> tracks, DiscToc? toc = null)
    {
        ArgumentNullException.ThrowIfNull(releaseInfo);
        ArgumentNullException.ThrowIfNull(tracks);

        var text = new StringBuilder();

        if (toc?.CatalogNumber is not null)
        {
            text.Append("CATALOG ").Append(toc.CatalogNumber).Append('\n');
        }

        text.Append("PERFORMER ").Append(Quote(releaseInfo.Artist)).Append('\n');
        text.Append("TITLE ").Append(Quote(releaseInfo.Title)).Append('\n');

        // Tracks whose TRACK/TITLE/PERFORMER/ISRC header already appeared as
        // the tail-end pregap of the *previous* track's FILE block (see
        // AppendPregapContinuation) -- their own loop iteration below must
        // only append INDEX 01, not repeat the header.
        var headerEmitted = new bool[tracks.Count];

        for (var i = 0; i < tracks.Count; i++)
        {
            var (track, fileName) = tracks[i];
            var tocTrack = FindTocTrack(toc, track.Number);

            text.Append("FILE ").Append(Quote(fileName)).Append(" WAVE\n");
            if (!headerEmitted[i])
            {
                AppendTrackHeader(text, track, tocTrack);
            }

            text.Append("    INDEX 01 00:00:00\n");

            if (i + 1 < tracks.Count && AppendPregapContinuation(text, tracks[i].Track, tocTrack, tracks[i + 1].Track, toc))
            {
                headerEmitted[i + 1] = true;
            }
        }

        return text.ToString();
    }

    /// <summary>Formats <paramref name="tracks"/> and writes it to <paramref name="path"/>.</summary>
    /// <param name="releaseInfo">The release this disc belongs to.</param>
    /// <param name="tracks">This disc's present tracks -- see <see cref="Format"/>.</param>
    /// <param name="path">The destination file path.</param>
    /// <param name="toc">This disc's physical table of contents -- see <see cref="Format"/>.</param>
    public static void Write(ReleaseInfo releaseInfo, IReadOnlyList<(TrackInfo Track, string FileName)> tracks, string path, DiscToc? toc = null) =>
        File.WriteAllText(path, Format(releaseInfo, tracks, toc));

    /// <summary>
    /// Appends the next track's <c>INDEX 00</c> pregap line -- and its
    /// header, since a pregap physically lives at the *tail* of the
    /// previous track's ripped file, not the head of its own (see root
    /// <c>CLAUDE.md</c> § Gotchas: "a track's ripped audio includes the
    /// following track's pregap") -- when all of the following hold:
    /// <paramref name="currentTrack"/> and <paramref name="nextTrack"/> are
    /// numerically consecutive (a degraded rip's gap in the matched list
    /// must not misattribute a pregap across missing tracks), the next
    /// track's pregap was actually scanned and is nonzero, and
    /// <paramref name="currentTocTrack"/> is known (needed for the offset
    /// arithmetic). This is also how track 1's own pregap -- reported by the
    /// <c>--fast-toc</c> scan <see cref="Rip.PipelineRunner"/> always uses,
    /// but never actually captured in any ripped file (there is no
    /// "previous track" to append it to; <c>cd-paranoia</c> reads exactly
    /// <c>[StartFrame, EndFrame]</c>) -- ends up never rendered: this method
    /// is only ever called looking *forward* from a real track, never for
    /// track 1's own leading gap.
    /// </summary>
    /// <param name="text">The buffer to append to.</param>
    /// <param name="currentTrack">The track whose <c>FILE</c> block is open.</param>
    /// <param name="currentTocTrack">The same track's physical frame range, or <see langword="null"/> if unknown.</param>
    /// <param name="nextTrack">The following track.</param>
    /// <param name="toc">The disc's table of contents, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the next track's header was emitted here (so the caller must not repeat it).</returns>
    private static bool AppendPregapContinuation(StringBuilder text, TrackInfo currentTrack, DiscTocTrack? currentTocTrack, TrackInfo nextTrack, DiscToc? toc)
    {
        if (nextTrack.Number != currentTrack.Number + 1 || currentTocTrack is null)
        {
            return false;
        }

        var nextTocTrack = FindTocTrack(toc, nextTrack.Number);
        if (nextTocTrack?.PregapFrames is not { } pregap || pregap <= 0)
        {
            return false;
        }

        var ownLength = currentTocTrack.EndFrame - currentTocTrack.StartFrame + 1;
        var offsetFrames = ownLength - pregap;
        if (offsetFrames < 0)
        {
            // A pregap longer than the track it's supposedly appended to is
            // internally inconsistent data -- skip rather than emit a
            // nonsensical negative-looking timecode.
            return false;
        }

        AppendTrackHeader(text, nextTrack, nextTocTrack);
        text.Append("    INDEX 00 ").Append(FormatTimecode(offsetFrames)).Append('\n');
        return true;
    }

    /// <summary>Appends one track's <c>TRACK</c>/<c>TITLE</c>/<c>PERFORMER</c>/<c>ISRC</c> header block.</summary>
    /// <param name="text">The buffer to append to.</param>
    /// <param name="track">The track to render.</param>
    /// <param name="tocTrack">The same track's physical data, for <c>ISRC</c> -- or <see langword="null"/> if unknown.</param>
    private static void AppendTrackHeader(StringBuilder text, TrackInfo track, DiscTocTrack? tocTrack)
    {
        text.Append("  TRACK ").Append(track.Number.ToString("D2", CultureInfo.InvariantCulture)).Append(" AUDIO\n");
        text.Append("    TITLE ").Append(Quote(track.Title)).Append('\n');
        text.Append("    PERFORMER ").Append(Quote(track.Artist)).Append('\n');
        if (tocTrack?.Isrc is not null)
        {
            text.Append("    ISRC ").Append(tocTrack.Isrc).Append('\n');
        }
    }

    /// <summary>Finds <paramref name="trackNumber"/>'s audio track in <paramref name="toc"/>, if known.</summary>
    /// <param name="toc">The disc's table of contents, or <see langword="null"/>.</param>
    /// <param name="trackNumber">The track number to look up.</param>
    /// <returns>The matching audio track, or <see langword="null"/> if <paramref name="toc"/> is null or has none.</returns>
    private static DiscTocTrack? FindTocTrack(DiscToc? toc, int trackNumber) =>
        toc?.Tracks.FirstOrDefault(t => t.TrackNumber == trackNumber && t.IsAudio);

    /// <summary>Converts a frame count to a cue sheet <c>MM:SS:FF</c> index timecode.</summary>
    /// <param name="frames">The frame count to format.</param>
    /// <returns>The formatted timecode.</returns>
    private static string FormatTimecode(int frames)
    {
        var f = frames % FramesPerSecond;
        var totalSeconds = frames / FramesPerSecond;
        var s = totalSeconds % 60;
        var m = totalSeconds / 60;
        return $"{m:D2}:{s:D2}:{f:D2}";
    }

    /// <summary>Quotes a cue sheet field value, replacing embedded double quotes (the format has no escape mechanism) with a single quote.</summary>
    /// <param name="value">The value to quote.</param>
    /// <returns>The quoted value.</returns>
    private static string Quote(string value) => "\"" + value.Replace('"', '\'') + "\"";
}
