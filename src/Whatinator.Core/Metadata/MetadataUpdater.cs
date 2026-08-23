using Whatinator.Core.Checksums;
using Whatinator.Core.Flac;
using Whatinator.Core.Mp3;
using Whatinator.Core.Naming;

namespace Whatinator.Core.Metadata;

/// <summary>
/// Applies a corrected <see cref="ReleaseInfo"/> to an already-packaged
/// release container folder (what <see cref="FlacPackager"/>/
/// <see cref="Mp3Packager"/> produced): backs up the metadata it
/// replaces, refreshes <c>id.txt</c> and <c>checksum_sha256.txt</c>, and
/// renames the folder if its computed name no longer matches (year or
/// artist/title correction). Does not touch individual audio files --
/// see <c>docs/plan/implementation/phase-009.md</c> § Scope decisions.
/// </summary>
public static class MetadataUpdater
{
    /// <summary>The backup filename the folder's previous <c>releaseinfo.json</c> is saved under before being overwritten.</summary>
    private const string BackupFileName = "releaseinfo.bak";

    /// <summary>Compares a release folder's existing metadata against an incoming replacement. Pure -- no I/O.</summary>
    /// <param name="oldReleaseInfo">The metadata currently on disk.</param>
    /// <param name="newReleaseInfo">The metadata about to replace it.</param>
    /// <returns>The comparison.</returns>
    public static MetadataChangeSummary DetectChange(ReleaseInfo oldReleaseInfo, ReleaseInfo newReleaseInfo)
    {
        ArgumentNullException.ThrowIfNull(oldReleaseInfo);
        ArgumentNullException.ThrowIfNull(newReleaseInfo);

        return new MetadataChangeSummary(
            oldReleaseInfo.Artist,
            newReleaseInfo.Artist,
            oldReleaseInfo.Title,
            newReleaseInfo.Title,
            ReleaseFolderNaming.ExtractYear(oldReleaseInfo.Date),
            ReleaseFolderNaming.ExtractYear(newReleaseInfo.Date));
    }

    /// <summary>Applies <paramref name="newReleaseInfo"/> to <paramref name="targetDirectory"/>.</summary>
    /// <param name="newReleaseInfo">The corrected metadata to apply.</param>
    /// <param name="targetDirectory">An already-packaged FLAC or MP3 release container folder.</param>
    /// <returns>Where everything ended up.</returns>
    /// <exception cref="FileNotFoundException"><paramref name="targetDirectory"/> has no existing <c>releaseinfo.json</c>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="targetDirectory"/> has neither <c>.flac</c> nor <c>.mp3</c> files, so its packaged format can't be inferred.</exception>
    public static MetadataUpdateResult Apply(ReleaseInfo newReleaseInfo, string targetDirectory)
    {
        ArgumentNullException.ThrowIfNull(newReleaseInfo);

        var releaseInfoPath = Path.Combine(targetDirectory, "releaseinfo.json");
        if (!File.Exists(releaseInfoPath))
        {
            throw new FileNotFoundException(
                $"'{targetDirectory}' has no releaseinfo.json -- not a packaged release folder.", releaseInfoPath);
        }

        var extension = InferAudioExtension(targetDirectory);

        var backupPath = Path.Combine(targetDirectory, BackupFileName);
        File.Copy(releaseInfoPath, backupPath, overwrite: true);

        ReleaseInfoFile.Save(newReleaseInfo, releaseInfoPath);
        IdTextFile.Write(newReleaseInfo, Path.Combine(targetDirectory, "id.txt"));

        var checksumFilePath = Path.Combine(targetDirectory, "checksum_sha256.txt");
        var checksumFileCount = WriteChecksums(targetDirectory, extension, checksumFilePath);

        var (finalDirectory, folderRenamed) = RenameIfNeeded(newReleaseInfo, targetDirectory, extension);

        return new MetadataUpdateResult(finalDirectory, backupPath, folderRenamed, checksumFilePath, checksumFileCount);
    }

    /// <summary>Determines whether <paramref name="directory"/> holds a packaged FLAC or MP3 release, by which audio extension is present.</summary>
    /// <param name="directory">The folder to inspect, recursively.</param>
    /// <returns>The audio file extension found (<c>".flac"</c> or <c>".mp3"</c>).</returns>
    /// <exception cref="InvalidOperationException">Neither extension has any files.</exception>
    private static string InferAudioExtension(string directory)
    {
        if (Directory.EnumerateFiles(directory, "*.flac", SearchOption.AllDirectories).Any())
        {
            return ".flac";
        }

        if (Directory.EnumerateFiles(directory, "*.mp3", SearchOption.AllDirectories).Any())
        {
            return ".mp3";
        }

        throw new InvalidOperationException(
            $"'{directory}' has neither .flac nor .mp3 files -- can't determine which release format to update.");
    }

    /// <summary>
    /// Rescans <paramref name="directory"/> for files matching
    /// <paramref name="extension"/> plus <c>.log</c> files and (re)writes the
    /// checksum manifest -- matching <see cref="FlacPackager"/>/
    /// <see cref="Mp3Packager"/>'s manifest scope (audio + log, not
    /// cover/id.txt/releaseinfo.json/.m3u -- see
    /// <c>docs/backlog-completed/003-compare-checksum-never-clean-on-packaged-folder.md</c>).
    /// </summary>
    /// <param name="directory">The release container folder.</param>
    /// <param name="extension">The audio extension to hash (<c>".flac"</c> or <c>".mp3"</c>).</param>
    /// <param name="checksumFilePath">The destination manifest path.</param>
    /// <returns>How many files were hashed.</returns>
    private static int WriteChecksums(string directory, string extension, string checksumFilePath)
    {
        var files = new[] { "*" + extension, "*.log" }
            .SelectMany(pattern => Directory.EnumerateFiles(directory, pattern, SearchOption.AllDirectories))
            .Select(path => (RelativePath: ToRelativePath(directory, path), AbsolutePath: path))
            .ToList();
        ChecksumFile.Write(files, checksumFilePath);
        return files.Count;
    }

    /// <summary>Renames <paramref name="targetDirectory"/> if its freshly-computed container name no longer matches its current name.</summary>
    /// <param name="newReleaseInfo">The metadata just applied.</param>
    /// <param name="targetDirectory">The folder to possibly rename.</param>
    /// <param name="extension">The inferred audio extension, used to pick the FLAC vs. MP3 naming convention.</param>
    /// <returns>The folder's final path, and whether it was renamed.</returns>
    private static (string FinalDirectory, bool Renamed) RenameIfNeeded(ReleaseInfo newReleaseInfo, string targetDirectory, string extension)
    {
        var expectedName = extension == ".flac"
            ? FlacFolderNaming.ContainerFolderName(newReleaseInfo)
            : Mp3FolderNaming.ContainerFolderName(newReleaseInfo);

        var trimmed = Path.TrimEndingDirectorySeparator(targetDirectory);
        var currentName = Path.GetFileName(trimmed);
        if (string.Equals(currentName, expectedName, StringComparison.Ordinal))
        {
            return (targetDirectory, false);
        }

        var parent = Path.GetDirectoryName(trimmed) ?? ".";
        var newPath = Path.Combine(parent, expectedName);
        Directory.Move(trimmed, newPath);
        return (newPath, true);
    }

    /// <summary>Converts an absolute path to a forward-slash-separated path relative to <paramref name="baseDir"/>.</summary>
    /// <param name="baseDir">The base directory.</param>
    /// <param name="fullPath">The absolute path to make relative.</param>
    /// <returns>The relative path, using <c>/</c> regardless of host OS.</returns>
    private static string ToRelativePath(string baseDir, string fullPath) =>
        Path.GetRelativePath(baseDir, fullPath).Replace(Path.DirectorySeparatorChar, '/');
}
