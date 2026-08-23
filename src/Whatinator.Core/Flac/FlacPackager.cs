using Whatinator.Core.Checksums;
using Whatinator.Core.CoverArt;
using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;
using Whatinator.Core.Rip;

namespace Whatinator.Core.Flac;

/// <summary>
/// Assembles the project's standard FLAC release folder from a single
/// <see cref="Whatinator.Core.Rip.WhatinatorRipRunner"/> rip and a resolved
/// <see cref="Whatinator.Core.Metadata.ReleaseInfo"/>. Moves the ripped
/// disc's FLAC files, any retained WAV files (see
/// <see cref="Whatinator.Core.Rip.WhatinatorRipOptions.KeepWav"/>), and a
/// <c>.log</c> file if one is present (byte for byte, untouched -- see root
/// <c>CLAUDE.md</c> § Gotchas; none exists yet as of phase 015, since
/// <see cref="Whatinator.Core.Rip.WhatinatorRipRunner"/> doesn't produce one
/// until phase 016) into place, then idempotently regenerates the
/// container-level artifacts (<c>releaseinfo.json</c>, <c>id.txt</c>,
/// <c>checksum_sha256.txt</c>, <c>.m3u</c>, cover art) by rescanning
/// whatever <c>.flac</c> files are currently present -- safe to call once
/// per disc of a multi-disc release, in any order, across separate
/// sessions. <c>checksum_sha256.txt</c> itself only covers <c>.flac</c> and
/// <c>.log</c> files, not the other container-level artifacts -- see
/// <see cref="WriteChecksums(string)"/>.
/// </summary>
public sealed class FlacPackager
{
    private readonly ICoverArtClient _coverArtClient;

    /// <summary>Initializes a new instance of the <see cref="FlacPackager"/> class.</summary>
    /// <param name="coverArtClient">The cover art client to use.</param>
    public FlacPackager(ICoverArtClient coverArtClient)
    {
        ArgumentNullException.ThrowIfNull(coverArtClient);
        _coverArtClient = coverArtClient;
    }

    /// <summary>Packages one disc's rip output into the project's standard FLAC folder layout.</summary>
    /// <param name="options">The packaging options.</param>
    /// <returns>Where everything ended up.</returns>
    /// <exception cref="ArgumentException"><paramref name="options"/>'s disc number is missing or out of range for a multi-disc release.</exception>
    /// <exception cref="DirectoryNotFoundException"><see cref="FlacPackageOptions.SourceDirectory"/> doesn't exist.</exception>
    /// <exception cref="InvalidOperationException">The source directory has no <c>.flac</c> files, or more than one <c>.log</c> file.</exception>
    public async Task<FlacPackageResult> PackageAsync(FlacPackageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(options.SourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: '{options.SourceDirectory}'.");
        }

        var releaseInfo = options.ReleaseInfo;
        var isMultiDisc = releaseInfo.Media.Count > 1;
        var discNumber = ReleaseFolderNaming.ResolveDiscNumber(releaseInfo, options.DiscNumber);

        var containerDir = Path.Combine(options.DestinationParentDirectory, ReleaseFolderNaming.ContainerFolderName(releaseInfo, "flac"));
        var discDir = isMultiDisc ? Path.Combine(containerDir, ReleaseFolderNaming.DiscFolderName(discNumber)) : containerDir;
        Directory.CreateDirectory(discDir);

        var movedFlacFiles = MoveFiles(options.SourceDirectory, discDir, "*.flac");
        if (movedFlacFiles.Count == 0)
        {
            throw new InvalidOperationException($"No .flac files found in '{options.SourceDirectory}'.");
        }

        MoveFiles(options.SourceDirectory, discDir, "*.wav");

        var logFilePath = MoveLogFile(options.SourceDirectory, discDir);

        ReleaseInfoFile.Save(releaseInfo, Path.Combine(containerDir, "releaseinfo.json"));
        IdTextFile.Write(releaseInfo, Path.Combine(containerDir, "id.txt"), options.DiscCatalogNumber);
        WriteChecksums(containerDir);
        WritePlaylist(releaseInfo, containerDir, isMultiDisc);

        var coverArtPath = await TryWriteCoverArtAsync(releaseInfo, containerDir).ConfigureAwait(false);

        return new FlacPackageResult(containerDir, discDir, movedFlacFiles.Count, logFilePath, coverArtPath);
    }

    /// <summary>Moves every file matching <paramref name="searchPattern"/> from <paramref name="sourceDir"/> into <paramref name="destDir"/>.</summary>
    /// <param name="sourceDir">The directory to move files out of.</param>
    /// <param name="destDir">The directory to move files into.</param>
    /// <param name="searchPattern">The file glob to match.</param>
    /// <returns>The moved files' destination paths.</returns>
    private static List<string> MoveFiles(string sourceDir, string destDir, string searchPattern)
    {
        var moved = new List<string>();
        foreach (var file in Directory.GetFiles(sourceDir, searchPattern))
        {
            var destination = Path.Combine(destDir, Path.GetFileName(file));
            File.Move(file, destination);
            moved.Add(destination);
        }

        return moved;
    }

    /// <summary>Moves a single <c>.log</c> file, if present, from <paramref name="sourceDir"/> into <paramref name="destDir"/>, unmodified.</summary>
    /// <param name="sourceDir">The directory to move the log out of.</param>
    /// <param name="destDir">The directory to move the log into.</param>
    /// <returns>The log's destination path, or <see langword="null"/> if the source directory had none.</returns>
    private static string? MoveLogFile(string sourceDir, string destDir)
    {
        var logFiles = Directory.GetFiles(sourceDir, "*.log");
        if (logFiles.Length == 0)
        {
            return null;
        }

        if (logFiles.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple .log files found in '{sourceDir}'; expected at most one.");
        }

        var destination = Path.Combine(destDir, Path.GetFileName(logFiles[0]));
        File.Move(logFiles[0], destination);
        return destination;
    }

    /// <summary>
    /// Rescans <paramref name="containerDir"/> for every <c>.flac</c> and
    /// <c>.log</c> file and (re)writes <c>checksum_sha256.txt</c>. Deliberately
    /// excludes <c>cover.*</c>, <c>id.txt</c>, <c>releaseinfo.json</c>, and
    /// <c>.m3u</c> -- see
    /// <c>docs/backlog-completed/003-compare-checksum-never-clean-on-packaged-folder.md</c>
    /// for why. A future <c>.cue</c> file (backlog 040) belongs in this
    /// pattern list too, once it exists.
    /// </summary>
    /// <param name="containerDir">The release's container folder.</param>
    private static void WriteChecksums(string containerDir)
    {
        var files = EnumerateManifestFiles(containerDir, "*.flac", "*.log")
            .Select(path => (RelativePath: ToRelativePath(containerDir, path), AbsolutePath: path));
        ChecksumFile.Write(files, Path.Combine(containerDir, "checksum_sha256.txt"));
    }

    /// <summary>Enumerates every file under <paramref name="containerDir"/> matching any of <paramref name="patterns"/>, recursively.</summary>
    /// <param name="containerDir">The directory to scan.</param>
    /// <param name="patterns">The file globs to match.</param>
    /// <returns>Matching absolute file paths.</returns>
    private static IEnumerable<string> EnumerateManifestFiles(string containerDir, params string[] patterns) =>
        patterns.SelectMany(pattern => Directory.EnumerateFiles(containerDir, pattern, SearchOption.AllDirectories));

    /// <summary>
    /// Rescans each medium's disc folder for <c>.flac</c> files and (re)writes
    /// the release's <c>.m3u</c>. A medium whose folder doesn't exist yet is
    /// omitted entirely; a medium missing only some tracks (a degraded rip --
    /// see <see cref="Whatinator.Core.Rip.WhatinatorRipResult.Degraded"/>)
    /// still contributes whichever tracks <see cref="TrackFileMatcher"/>
    /// actually found files for, rather than being omitted until every track
    /// is present.
    /// </summary>
    /// <param name="releaseInfo">The release being packaged.</param>
    /// <param name="containerDir">The release's container folder.</param>
    /// <param name="isMultiDisc">Whether discs live in <c>cd1/</c>/<c>cd2/</c> subfolders or flat in <paramref name="containerDir"/>.</param>
    private static void WritePlaylist(ReleaseInfo releaseInfo, string containerDir, bool isMultiDisc)
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

            var flacFiles = Directory.GetFiles(mediumDir, "*.flac");
            foreach (var (track, file) in TrackFileMatcher.Match(flacFiles, medium.Tracks))
            {
                entries.Add((
                    ToRelativePath(containerDir, file),
                    track.Artist,
                    track.Title,
                    (int)track.Duration.TotalSeconds));
            }
        }

        var m3uPath = Path.Combine(containerDir, ReleaseFolderNaming.ReleaseDisplayName(releaseInfo) + ".m3u");
        M3uPlaylist.Write(entries, m3uPath);
    }

    /// <summary>Fetches and saves cover art if none is already present in <paramref name="containerDir"/>.</summary>
    /// <param name="releaseInfo">The release being packaged.</param>
    /// <param name="containerDir">The release's container folder.</param>
    /// <returns>Where the cover art was saved, or <see langword="null"/> if skipped/unavailable.</returns>
    private async Task<string?> TryWriteCoverArtAsync(ReleaseInfo releaseInfo, string containerDir)
    {
        if (Directory.EnumerateFiles(containerDir, "cover.*").Any())
        {
            return null;
        }

        var coverArt = await _coverArtClient.TryDownloadFrontCoverAsync(releaseInfo.MusicBrainzReleaseId).ConfigureAwait(false);
        if (coverArt is null)
        {
            return null;
        }

        coverArt = await CoverArtProcessor.ProcessAsync(coverArt).ConfigureAwait(false);

        var path = Path.Combine(containerDir, "cover" + coverArt.FileExtension);
        await File.WriteAllBytesAsync(path, coverArt.Content).ConfigureAwait(false);
        return path;
    }

    /// <summary>Converts an absolute path to a forward-slash-separated path relative to <paramref name="baseDir"/>.</summary>
    /// <param name="baseDir">The base directory.</param>
    /// <param name="fullPath">The absolute path to make relative.</param>
    /// <returns>The relative path, using <c>/</c> regardless of host OS.</returns>
    private static string ToRelativePath(string baseDir, string fullPath) =>
        Path.GetRelativePath(baseDir, fullPath).Replace(Path.DirectorySeparatorChar, '/');
}
