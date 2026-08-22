using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Whatinator.Core.AccurateRip;
using Whatinator.Core.Drive;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Rip;

/// <summary>
/// Writes the project-authored rip log modeled on EAC's own log format
/// (<c>example/Glorilla - Glorious.log</c>, <c>example/Chappell Roan - The
/// Rise and Fall of a Midwest Princess.log</c>) -- see
/// <c>docs/plan/implementation/phase-016.md</c> for the section-by-section
/// research this is built from. Replaces "copy the prior rip path's own
/// <c>.log</c> verbatim" (phase 006's decision, now superseded -- see root
/// <c>CLAUDE.md</c> § Gotchas): there's no external tool's log to copy
/// anymore, so this project authors its own, carrying the same substance
/// (drive/offset info, full TOC, per-track peak/quality/speed/CRC/
/// AccurateRip results, a conclusive summary, a self-check hash) in EAC's
/// section shape instead of that prior YAML format. Deliberately never
/// emits a CUETools DB section -- this project never submits or queries that
/// service, a permanent boundary, not a phased deferral (see phase 016's
/// plan doc § Scope decisions).
/// </summary>
public static class WhatinatorEacLog
{
    /// <summary>CD frames per second.</summary>
    private const int FramesPerSecond = 75;

    /// <summary>Exact column header for the TOC table, copied byte-for-byte from a real EAC log.</summary>
    private const string TocHeader = "     Track |   Start  |  Length  | Start sector | End sector ";

    /// <summary>Exact separator row for the TOC table, copied byte-for-byte from a real EAC log.</summary>
    private const string TocSeparator = "    ---------------------------------------------------------";

    /// <summary>Formats <paramref name="options"/> as the full rip log text, ending with a self-check SHA-256 footer.</summary>
    /// <param name="options">The rip/drive/tool data to render.</param>
    /// <returns>The formatted log text.</returns>
    public static string Format(EacLogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var text = new StringBuilder();
        AppendHeader(text, options);
        AppendSettings(text, options);
        AppendEncoderSettings(text, options);
        AppendToc(text, options);
        AppendTracks(text, options);
        AppendSummary(text, options);

        // A plain SHA-256 self-check over everything above this line, in
        // EAC's own "==== Log checksum {HEX} ====" bracket shape -- not a
        // reimplementation of EAC's real (reverse-engineered, proprietary)
        // checksum algorithm. Same tamper-evidence mechanism as the prior
        // rip path's own "SHA-256 hash: {hex}" footer this replaces, just
        // reformatted.
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        text.Append($"==== Log checksum {hash} ====\n");

        return text.ToString();
    }

    /// <summary>Formats <paramref name="options"/> and writes it to <paramref name="path"/>.</summary>
    /// <param name="options">The rip/drive/tool data to render.</param>
    /// <param name="path">The destination file path.</param>
    public static void Write(EacLogOptions options, string path) => File.WriteAllText(path, Format(options));

    /// <summary>Appends the header block: whatinator/date identification, release, OS info, drive, and read mode.</summary>
    private static void AppendHeader(StringBuilder text, EacLogOptions o)
    {
        // Branded as whatinator, not "Exact Audio Copy" -- this log matches
        // EAC's section shape, but claiming the literal product name would
        // misrepresent what actually produced it.
        WhatinatorLogHeader.Append(text, o.StartTime);
        text.Append('\n');
        text.Append($"{o.ReleaseInfo.Artist} / {o.ReleaseInfo.Title}\n");
        text.Append('\n');

        // Not part of real EAC's log (a Windows tool with no uname/os-release
        // concept) but explicitly required by init.md's flac-log spec -- kept
        // as its own small block rather than forced into an EAC-shaped line.
        text.Append($"OS: {o.Uname}\n");
        text.Append($"OS (pretty name): {o.OsPrettyName ?? "-"}\n");
        text.Append($"Rip started: {FormatTimestamp(o.StartTime)}\n");
        text.Append($"Rip finished: {FormatTimestamp(o.EndTime)}\n");
        text.Append('\n');

        var drive = $"{o.DriveVendor ?? "unknown"} {o.DriveModel ?? "unknown"} (revision {o.DriveRelease ?? "unknown"})";
        text.Append($"Used drive  : {drive}   Device: {o.DevicePath}\n");
        text.Append('\n');

        // The disc's own CATALOG line (UPC/EAN), from DiscToc -- distinct
        // from any MusicBrainz label catalog number, which this log never
        // prints (see root CLAUDE.md's ISRC/UPC gotcha).
        text.Append($"Disc catalogue number (UPC/EAN)             : {o.Toc.CatalogNumber ?? "none"}\n");
        text.Append('\n');

        // WhatinatorRipRunner (phase 015) always does the cd-paranoia
        // test+copy double-read cycle, EAC's own Burst/Secure distinction
        // boiling down to whether that verification happened -- so this is
        // always Secure for whatinator's own rips (the real examples on
        // hand show Burst because those source rips skipped verification,
        // not a mode whatinator ever produces).
        text.Append("Read mode : Secure\n");
    }

    /// <summary>Appends the rip settings block (offset/overread/cache/interface).</summary>
    private static void AppendSettings(StringBuilder text, EacLogOptions o)
    {
        text.Append('\n');
        AppendSetting(text, "Read offset correction", o.ReadOffset.ToString(CultureInfo.InvariantCulture));
        AppendSetting(text, "Overread into Lead-In and Lead-Out", o.Overread ? "Yes" : "No");

        // The next two toggles have no cd-paranoia equivalent (phase 016
        // scope decision) -- pinned to the values that match how
        // CdParanoiaTrackReader/AccurateRipChecksum actually treat a track's
        // raw PCM data (the full range is always kept, null samples and
        // all).
        AppendSetting(text, "Fill up missing offset samples with silence", "Yes");
        AppendSetting(text, "Delete leading and trailing silent blocks", "No");
        AppendSetting(text, "Null samples used in CRC calculations", "Yes");

        // CdParanoiaVersion/CdrdaoVersion already carry the tool's own name
        // (SystemInfo.GetCdParanoiaVersion/GetCdrdaoVersion capture the
        // whole first line of the real --version banner) -- no separate
        // "cd-paranoia "/"cdrdao " prefix needed.
        AppendSetting(text, "Used interface", $"{o.CdParanoiaVersion} (libcdio-paranoia)");
        AppendSetting(text, "Gap handling", "Appended to previous track");
        AppendSetting(text, "Gap detection", o.CdrdaoVersion);
        var defeatAudioCache = o.CacheDefeat switch
        {
            CacheDefeatResult.CanDefeat => "Yes",
            CacheDefeatResult.CannotDefeat => "No",
            _ => "Unknown",
        };
        AppendSetting(text, "Defeat audio cache", defeatAudioCache);
    }

    /// <summary>Appends the encoder settings block (flac invocation shape).</summary>
    private static void AppendEncoderSettings(StringBuilder text, EacLogOptions o)
    {
        text.Append('\n');
        AppendEncoderSetting(text, "Used output format", "FLAC");

        // FLAC has neither a bitrate nor EAC's exact notion of "Quality" --
        // substituted with flac's own compression-level setting.
        // FlacEncoder.BuildStartInfo (phase 015) passes no -0..-8 flag, so
        // this is flac's own documented default.
        AppendEncoderSetting(text, "Quality", "-5 (flac default compression level)");
        AppendEncoderSetting(text, "Add ID3 tag", "No");

        // FlacVersion already carries the tool's own name ("flac 1.5.0").
        AppendEncoderSetting(text, "Command line compressor", o.FlacVersion);

        // Mirrors FlacEncoder.BuildStartInfo's exact flag order -- this is a
        // whole-disc settings line, not tied to one track, so per-track
        // values are shown as placeholders rather than one specific track's
        // resolved values.
        const string flacArgs =
            "--verify -o <output.flac> -T ARTIST=<artist> -T ALBUM=<album> -T TITLE=<title> " +
            "-T ALBUMARTIST=<albumartist> -T DATE=<year> -T TRACKNUMBER=<n> -T TRACKTOTAL=<total> " +
            "-T ISRC=<isrc> -T GENRE=<genre> <input.wav>";
        AppendEncoderSetting(text, "Additional command line options", flacArgs);
    }

    /// <summary>Appends the whole-disc TOC table.</summary>
    private static void AppendToc(StringBuilder text, EacLogOptions o)
    {
        text.Append('\n');
        text.Append('\n');
        text.Append("TOC of the extracted CD\n");
        text.Append('\n');
        text.Append(TocHeader).Append('\n');
        text.Append(TocSeparator).Append('\n');

        foreach (var track in o.Toc.Tracks.Where(t => t.IsAudio))
        {
            // EAC's TOC "Start"/"Start sector" is pregap-inclusive (the gap
            // is shown as belonging to the following track's nominal start,
            // matching "Gap handling: Appended to previous track" above) --
            // DiscTocTrack.StartFrame is post-pregap (index 1), so the
            // pregap is subtracted back out here.
            var startSector = track.StartFrame - (track.PregapFrames ?? 0);
            var endSector = track.EndFrame;
            var length = endSector - startSector + 1;

            text.Append(track.TrackNumber.ToString(CultureInfo.InvariantCulture).PadLeft(9)).Append("  |");
            text.Append(' ').Append(FormatMsf(startSector).PadLeft(8)).Append(" |");
            text.Append(' ').Append(FormatMsf(length).PadLeft(8)).Append(" |");
            text.Append(startSector.ToString(CultureInfo.InvariantCulture).PadLeft(10)).Append("    |");
            text.Append(endSector.ToString(CultureInfo.InvariantCulture).PadLeft(9)).Append("   \n");
        }
    }

    /// <summary>Appends one block per ripped track.</summary>
    private static void AppendTracks(StringBuilder text, EacLogOptions o)
    {
        var audioTracks = o.Toc.Tracks.Where(t => t.IsAudio).ToList();

        foreach (var trackResult in o.RipResult.Tracks)
        {
            text.Append('\n');
            text.Append('\n');
            text.Append($"Track {trackResult.TrackNumber,2}\n");

            if (trackResult.Degraded)
            {
                // Not an EAC scenario (EAC never partially rips a disc) --
                // whatinator's own extension of the format, per init.md's
                // "allow bad data capture just to get through capturing the
                // cd" (WhatinatorRipResult.Degraded).
                text.Append('\n');
                text.Append($"     [WARNING] Track could not be read after {trackResult.Attempts} attempt(s) - no data captured\n");
                continue;
            }

            var tocTrack = audioTracks.Single(t => t.TrackNumber == trackResult.TrackNumber);
            var fileName = Path.GetFileName(trackResult.FlacFilePath ?? trackResult.WavFilePath!);
            var filePath = Path.Combine(o.DiscDirectory, fileName);

            text.Append('\n');
            text.Append($"     Filename {filePath}\n");

            var pregap = tocTrack.PregapFrames ?? 0;
            if (pregap > 0)
            {
                text.Append('\n');
                text.Append($"     Pre-gap length  {FormatHmsf(pregap)}\n");
            }

            if (tocTrack.Isrc is not null)
            {
                text.Append('\n');
                text.Append($"     ISRC {tocTrack.Isrc}\n");
            }

            text.Append('\n');
            text.Append($"     Peak level {FormatPeak(trackResult.Peak)}\n");
            text.Append($"     Extraction speed {FormatSpeed(trackResult, tocTrack)}\n");
            text.Append($"     Test CRC {trackResult.Crc32:X8}\n");
            text.Append($"     Copy CRC {trackResult.Crc32:X8}\n");
            text.Append($"     {FormatAccurateRipLine(o.RipResult.AccurateRipFound, trackResult.AccurateRip)}\n");

            // Only reachable once the test/copy CRC32s have already matched
            // (see WhatinatorRipRunner) -- a track that didn't verify never
            // gets here, it's Degraded above instead. Matches both real
            // example logs on hand, which show this on every track.
            text.Append("     Copy OK\n");
        }
    }

    /// <summary>Appends the conclusive summary block and footer (minus the trailing checksum line -- see <see cref="Format"/>).</summary>
    private static void AppendSummary(StringBuilder text, EacLogOptions o)
    {
        text.Append('\n');
        text.Append('\n');
        text.Append(FormatAccurateRipSummary(o.RipResult)).Append('\n');
        text.Append('\n');
        text.Append(o.RipResult.Degraded ? "Some tracks were not ripped (skipped)" : "No errors occurred").Append('\n');
        text.Append('\n');
        text.Append("End of status report\n");
        text.Append('\n');
    }

    /// <summary>
    /// Builds the AccurateRip summary sentence -- modeled on real EAC
    /// output -- the zero-hit case matches word-for-word in
    /// both a real EAC log (<c>Glorilla - Glorious.log</c>) and that source;
    /// see root <c>CLAUDE.md</c> § Gotchas for the full research trail).
    /// </summary>
    private static string FormatAccurateRipSummary(WhatinatorRipResult result)
    {
        if (!result.AccurateRipFound)
        {
            return "None of the tracks are present in the AccurateRip database";
        }

        var nonDegradedCount = result.Tracks.Count(t => !t.Degraded);
        var accuratelyRipped = result.Tracks.Count(t => t.AccurateRip?.IsMatch == true);

        if (accuratelyRipped == 0)
        {
            return "No tracks could be verified as accurate (you may have a different pressing from the one(s) in the database)";
        }

        if (accuratelyRipped < nonDegradedCount)
        {
            var unmatched = nonDegradedCount - accuratelyRipped;
            return $"Some tracks could not be verified as accurate ({unmatched}/{nonDegradedCount} got no match)";
        }

        return "All tracks accurately ripped";
    }

    /// <summary>Builds one track's AccurateRip result line.</summary>
    private static string FormatAccurateRipLine(bool discAccurateRipFound, AccurateRipTrackMatch? match)
    {
        if (match is null)
        {
            // The whole-disc lookup was never attempted (some other track on
            // this disc was Degraded -- see WhatinatorRipResult.AccurateRipFound
            // remarks). Not a scenario either real example log demonstrates.
            return "AccurateRip verification skipped (one or more tracks on this disc could not be read)";
        }

        if (!discAccurateRipFound)
        {
            // Confirmed verbatim against a real zero-hit EAC log
            // (Glorilla - Glorious.log) and the prior research described above.
            return "Track not present in AccurateRip database";
        }

        if (match.IsMatch)
        {
            var (confidence, crc, version) = match.ConfidenceV2 is not null
                ? (match.ConfidenceV2, match.MatchedCrcV2, "v2")
                : (match.ConfidenceV1, match.MatchedCrcV1, "v1");

            // Confirmed verbatim against a real AccurateRip-hit EAC log
            // (Chappell Roan - The Rise and Fall of a Midwest Princess.log).
            return $"Accurately ripped (confidence {confidence})  [{crc}]  (AR {version})";
        }

        if (match.MaxConfidence is not null)
        {
            // Not confirmed against a real EAC mismatch log -- none on hand
            // as of phase 016 (see docs/plan/implementation/phase-016.md §
            // Research findings item 7 / docs/plan/open_questions.md).
            // Well-documented public EAC convention, not verified firsthand.
            return $"Cannot be verified as accurate (confidence {match.MaxConfidence})  [{match.MaxConfidenceCrc}]";
        }

        return "Track not present in AccurateRip database";
    }

    /// <summary>Formats a peak sample level (0-32767) as EAC's percentage-of-full-scale, one decimal place.</summary>
    private static string FormatPeak(int? peak)
    {
        if (peak is null)
        {
            return "-";
        }

        var percent = (peak.Value / 32768.0) * 100;
        return $"{percent.ToString("0.0", CultureInfo.InvariantCulture)} %";
    }

    /// <summary>Formats a track's extraction speed as a multiple of realtime, e.g. <c>16.0 X</c>.</summary>
    private static string FormatSpeed(WhatinatorTrackRipResult track, DiscTocTrack tocTrack)
    {
        if (track.ElapsedTime is not { } elapsed || elapsed.TotalSeconds <= 0)
        {
            return "-";
        }

        var trackSeconds = (tocTrack.EndFrame - tocTrack.StartFrame + 1) / (double)FramesPerSecond;
        var speed = trackSeconds / elapsed.TotalSeconds;
        return $"{speed.ToString("0.0", CultureInfo.InvariantCulture)} X";
    }

    /// <summary>Appends one <c>{label,-44}: {value}</c> settings-block line.</summary>
    private static void AppendSetting(StringBuilder text, string label, string value) =>
        text.Append(label.PadRight(44)).Append(": ").Append(value).Append('\n');

    /// <summary>Appends one <c>{label,-32}: {value}</c> encoder-settings-block line.</summary>
    private static void AppendEncoderSetting(StringBuilder text, string label, string value) =>
        text.Append(label.PadRight(32)).Append(": ").Append(value).Append('\n');

    /// <summary>Formats a timestamp as ISO 8601 with an explicit UTC offset -- same shape as <see cref="Mp3.Mp3LogFile"/>'s.</summary>
    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

    /// <summary>Converts a frame count to <c>m:ss.ff</c> (no leading zero on minutes, no hours) -- the TOC table's own time format.</summary>
    private static string FormatMsf(int frames)
    {
        var f = frames % FramesPerSecond;
        var totalSeconds = frames / FramesPerSecond;
        var s = totalSeconds % 60;
        var m = totalSeconds / 60;
        return $"{m}:{s:D2}.{f:D2}";
    }

    /// <summary>Converts a frame count to <c>h:mm:ss.ff</c> (no leading zero on hours) -- the per-track pregap-length format.</summary>
    private static string FormatHmsf(int frames)
    {
        var f = frames % FramesPerSecond;
        var totalSeconds = frames / FramesPerSecond;
        var s = totalSeconds % 60;
        var totalMinutes = totalSeconds / 60;
        var m = totalMinutes % 60;
        var h = totalMinutes / 60;
        return $"{h}:{m:D2}:{s:D2}.{f:D2}";
    }
}
