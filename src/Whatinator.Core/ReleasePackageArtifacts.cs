using Whatinator.Core.Checksums;
using Whatinator.Core.Flac;
using Whatinator.Core.Metadata;
using Whatinator.Core.Mp3;
using Whatinator.Core.Naming;
using Whatinator.Core.Rip;

namespace Whatinator.Core;

/// <summary>
/// Writes and idempotently rescans the container-level artifacts a packaged
/// release folder carries alongside its audio files: <c>releaseinfo.json</c>,
/// <c>id.txt</c>, <c>checksum_sha256.txt</c>, and the <c>.m3u</c> playlist.
/// Shared by <see cref="FlacPackager"/> and <see cref="Mp3Packager"/>
/// (parameterized by audio extension so one implementation covers both) and
/// by <see cref="MetadataUpdater"/>'s checksum refresh -- see root
/// <c>CLAUDE.md</c> § "Packaging is idempotent by rescan". Never derive any
/// of these from only the disc currently being packaged; always rescan
/// what's actually on disk, so this stays safe to call once per disc of a
/// multi-disc release, in any order, across separate sessions.
/// </summary>
public static class ReleasePackageArtifacts
{
    /// <summary>
    /// Writes the full container-level artifact sequence:
    /// <c>releaseinfo.json</c>, <c>id.txt</c>, then rescans for the checksum
    /// manifest and playlist.
    /// </summary>
    /// <param name="releaseInfo">The release being packaged.</param>
    /// <param name="containerDir">The release's container folder.</param>
    /// <param name="isMultiDisc">Whether discs live in <c>cd1/</c>/<c>cd2/</c> subfolders or flat in <paramref name="containerDir"/>.</param>
    /// <param name="audioExtension">The packaged audio file extension, including the leading dot (<c>".flac"</c> or <c>".mp3"</c>).</param>
    /// <param name="upc">The disc's UPC/EAN catalogue number for <c>id.txt</c>, or <see langword="null"/> if unknown -- see <see cref="IdTextFile.Format"/>.</param>
    public static void Write(ReleaseInfo releaseInfo, string containerDir, bool isMultiDisc, string audioExtension, string? upc = null)
    {
        ReleaseInfoFile.Save(releaseInfo, Path.Combine(containerDir, "releaseinfo.json"));
        IdTextFile.Write(releaseInfo, Path.Combine(containerDir, "id.txt"), upc);
        WriteChecksums(containerDir, audioExtension);
        WritePlaylist(releaseInfo, containerDir, isMultiDisc, audioExtension);
    }

    /// <summary>
    /// Rescans <paramref name="containerDir"/> for every file matching
    /// <paramref name="audioExtension"/>, <c>.log</c>, and <c>.cue</c> and
    /// (re)writes <c>checksum_sha256.txt</c>. Deliberately excludes
    /// <c>cover.*</c>, <c>id.txt</c>, <c>releaseinfo.json</c>, and
    /// <c>.m3u</c> -- see
    /// <c>docs/backlog-completed/003-compare-checksum-never-clean-on-packaged-folder.md</c>
    /// for why. The <c>.cue</c> pattern only ever matches anything in a FLAC
    /// folder (<see cref="Flac.FlacPackager"/> is the only writer -- see
    /// <see cref="Whatinator.Core.CueSheetFile"/>), so it's harmless to
    /// include here unconditionally for the MP3 rescan too.
    /// </summary>
    /// <param name="containerDir">The release's container folder.</param>
    /// <param name="audioExtension">The packaged audio file extension, including the leading dot.</param>
    /// <returns>How many files were hashed.</returns>
    public static int WriteChecksums(string containerDir, string audioExtension)
    {
        var files = EnumerateManifestFiles(containerDir, "*" + audioExtension, "*.log", "*.cue")
            .Select(path => (RelativePath: ChecksumFile.ToRelativePath(containerDir, path), AbsolutePath: path))
            .ToList();
        ChecksumFile.Write(files, Path.Combine(containerDir, "checksum_sha256.txt"));
        return files.Count;
    }

    /// <summary>
    /// Rescans each medium's disc folder for files matching
    /// <paramref name="audioExtension"/> and (re)writes the release's
    /// <c>.m3u</c>. A medium whose folder doesn't exist yet is omitted
    /// entirely; a medium missing only some tracks (a degraded rip -- see
    /// <see cref="Whatinator.Core.Rip.WhatinatorRipResult.Degraded"/>) still
    /// contributes whichever tracks <see cref="TrackFileMatcher"/> actually
    /// found files for, rather than being omitted until every track is
    /// present.
    /// </summary>
    /// <param name="releaseInfo">The release being packaged.</param>
    /// <param name="containerDir">The release's container folder.</param>
    /// <param name="isMultiDisc">Whether discs live in <c>cd1/</c>/<c>cd2/</c> subfolders or flat in <paramref name="containerDir"/>.</param>
    /// <param name="audioExtension">The packaged audio file extension, including the leading dot.</param>
    public static void WritePlaylist(ReleaseInfo releaseInfo, string containerDir, bool isMultiDisc, string audioExtension)
    {
        var entries = new List<(string RelativePath, string Artist, string Title, int DurationSeconds)>();

        foreach (var medium in releaseInfo.Media.OrderBy(m => m.Position))
        {
            var mediumDir = isMultiDisc
                ? Path.Combine(containerDir, ReleaseFolderNaming.DiscFolderName(medium.Position))
                : containerDir;
            if (!Directory.Exists(mediumDir))
            {
                continue;
            }

            var audioFiles = Directory.GetFiles(mediumDir, "*" + audioExtension);
            foreach (var (track, file) in TrackFileMatcher.Match(audioFiles, medium.Tracks))
            {
                entries.Add((
                    ChecksumFile.ToRelativePath(containerDir, file),
                    track.Artist,
                    track.Title,
                    (int)track.Duration.TotalSeconds));
            }
        }

        var m3uPath = Path.Combine(containerDir, ReleaseFolderNaming.ReleaseDisplayName(releaseInfo) + ".m3u");
        M3uPlaylist.Write(entries, m3uPath);
    }

    /// <summary>Enumerates every file under <paramref name="containerDir"/> matching any of <paramref name="patterns"/>, recursively.</summary>
    /// <param name="containerDir">The directory to scan.</param>
    /// <param name="patterns">The file globs to match.</param>
    /// <returns>Matching absolute file paths.</returns>
    private static IEnumerable<string> EnumerateManifestFiles(string containerDir, params string[] patterns) =>
        patterns.SelectMany(pattern => Directory.EnumerateFiles(containerDir, pattern, SearchOption.AllDirectories));
}
