using Whatinator.Core.CoverArt;
using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;
using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

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
/// until phase 016) into place, writes this disc's <c>.cue</c> sheet (see
/// <see cref="Whatinator.Core.CueSheetFile"/>) alongside them, then
/// idempotently regenerates the container-level artifacts
/// (<c>releaseinfo.json</c>, <c>id.txt</c>, <c>checksum_sha256.txt</c>,
/// <c>.m3u</c>, cover art) by rescanning whatever <c>.flac</c> files are
/// currently present -- safe to call once per disc of a multi-disc release,
/// in any order, across separate sessions. Unlike those container-level
/// artifacts, the <c>.cue</c> sheet is written once per disc from that
/// call's own <see cref="FlacPackageOptions.Toc"/> rather than being
/// derivable from a directory rescan alone (a physical disc fact, not
/// something the filesystem records). <c>checksum_sha256.txt</c> covers
/// <c>.flac</c>, <c>.log</c>, and <c>.cue</c> files, not the other
/// container-level artifacts -- see
/// <see cref="Whatinator.Core.ReleasePackageArtifacts.WriteChecksums"/>.
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
    /// <param name="cancellationToken">A token to cancel the cover art download.</param>
    /// <returns>Where everything ended up.</returns>
    /// <exception cref="ArgumentException"><paramref name="options"/>'s disc number is missing or out of range for a multi-disc release.</exception>
    /// <exception cref="DirectoryNotFoundException"><see cref="FlacPackageOptions.SourceDirectory"/> doesn't exist.</exception>
    /// <exception cref="InvalidOperationException">The source directory has no <c>.flac</c> files, or more than one <c>.log</c> file.</exception>
    public async Task<FlacPackageResult> PackageAsync(FlacPackageOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Directory.Exists(options.SourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: '{options.SourceDirectory}'.");
        }

        var releaseInfo = options.ReleaseInfo;
        var isMultiDisc = releaseInfo.Media.Count > 1;
        var discNumber = ReleaseFolderNaming.ResolveDiscNumber(releaseInfo, options.DiscNumber);

        var (containerDir, discDir) = ReleaseFolderNaming.ResolveDiscDirectory(releaseInfo, options.DestinationParentDirectory, "flac", discNumber);
        Directory.CreateDirectory(discDir);

        var movedFlacFiles = MoveFiles(options.SourceDirectory, discDir, "*.flac");
        if (movedFlacFiles.Count == 0)
        {
            throw new InvalidOperationException($"No .flac files found in '{options.SourceDirectory}'.");
        }

        MoveFiles(options.SourceDirectory, discDir, "*.wav");

        var logFilePath = MoveLogFile(options.SourceDirectory, discDir);

        var medium = releaseInfo.Media.Single(m => m.Position == discNumber);
        var cueFilePath = WriteCueSheet(releaseInfo, medium, options.Toc, discDir);

        ReleasePackageArtifacts.Write(releaseInfo, containerDir, isMultiDisc, ".flac", options.DiscCatalogNumber, options.DiscIdMatched);

        var coverArtPath = await TryWriteCoverArtAsync(releaseInfo, containerDir, cancellationToken).ConfigureAwait(false);

        return new FlacPackageResult(containerDir, discDir, movedFlacFiles.Count, logFilePath, coverArtPath, cueFilePath);
    }

    /// <summary>
    /// Rescans <paramref name="discDir"/> for <c>.flac</c> files and writes
    /// this disc's <c>.cue</c> sheet, matched against <paramref name="medium"/>'s
    /// tracks the same way <see cref="ReleasePackageArtifacts.WritePlaylist"/>
    /// matches its <c>.m3u</c> entries -- a degraded rip's missing tracks are
    /// simply absent from the sheet rather than blocking it.
    /// </summary>
    /// <param name="releaseInfo">The release being packaged.</param>
    /// <param name="medium">This disc's track listing.</param>
    /// <param name="toc">This disc's physical table of contents, or <see langword="null"/> if unavailable.</param>
    /// <param name="discDir">The directory this disc's FLAC files were moved into.</param>
    /// <returns>Where the <c>.cue</c> sheet was written, or <see langword="null"/> if no track had a matching file.</returns>
    private static string? WriteCueSheet(ReleaseInfo releaseInfo, MediumInfo medium, DiscToc? toc, string discDir)
    {
        var matches = TrackFileMatcher.Match(Directory.GetFiles(discDir, "*.flac"), medium.Tracks);
        if (matches.Count == 0)
        {
            return null;
        }

        var cueTracks = matches.Select(m => (m.Track, Path.GetFileName(m.FilePath))).ToList();
        var cuePath = Path.Combine(discDir, ReleaseFolderNaming.ReleaseDisplayName(releaseInfo) + ".cue");
        CueSheetFile.Write(releaseInfo, cueTracks, cuePath, toc);
        return cuePath;
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

    /// <summary>Fetches and saves cover art if none is already present in <paramref name="containerDir"/>.</summary>
    /// <param name="releaseInfo">The release being packaged.</param>
    /// <param name="containerDir">The release's container folder.</param>
    /// <param name="cancellationToken">A token to cancel the cover art download.</param>
    /// <returns>Where the cover art was saved, or <see langword="null"/> if skipped/unavailable.</returns>
    private async Task<string?> TryWriteCoverArtAsync(ReleaseInfo releaseInfo, string containerDir, CancellationToken cancellationToken)
    {
        if (Directory.EnumerateFiles(containerDir, "cover.*").Any())
        {
            return null;
        }

        var coverArt = await _coverArtClient.TryDownloadFrontCoverAsync(releaseInfo.MusicBrainzReleaseId, cancellationToken).ConfigureAwait(false);
        if (coverArt is null)
        {
            return null;
        }

        coverArt = await CoverArtProcessor.ProcessAsync(coverArt, cancellationToken).ConfigureAwait(false);

        var path = Path.Combine(containerDir, "cover" + coverArt.FileExtension);
        await File.WriteAllBytesAsync(path, coverArt.Content).ConfigureAwait(false);
        return path;
    }
}
