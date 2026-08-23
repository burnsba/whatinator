using Whatinator.Core.Flac;
using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;
using Whatinator.Core.Rip;

namespace Whatinator.Core.Mp3;

/// <summary>
/// Assembles the project's standard MP3 release folder from a
/// <see cref="FlacPackager"/>-produced FLAC disc folder and a resolved
/// <see cref="Whatinator.Core.Metadata.ReleaseInfo"/>. Encodes every <c>.flac</c>
/// file that <see cref="TrackFileMatcher"/> can pair to a track number to
/// V0 MP3 via <see cref="LameEncoder"/> (tagged, but without cover art
/// embedded in the ID3 tag -- user decision, phase 007 UAT) -- a degraded
/// FLAC folder (fewer files than the disc's track count, from a rip
/// phase 008 let skip unripped tracks) still encodes whatever tracks are
/// present rather than failing the whole disc. Copies the FLAC folder's
/// cover art alongside the MP3s at the container level rather than
/// re-fetching it, then idempotently regenerates the container-level
/// artifacts (<c>releaseinfo.json</c>, <c>id.txt</c>,
/// <c>checksum_sha256.txt</c>, <c>.m3u</c>) by rescanning whatever
/// <c>.mp3</c> files are currently present -- safe to call once per disc of
/// a multi-disc release, in any order, across separate sessions.
/// <c>checksum_sha256.txt</c> itself only covers <c>.mp3</c> and <c>.log</c>
/// files, not the other container-level artifacts -- see
/// <see cref="Whatinator.Core.ReleasePackageArtifacts.WriteChecksums"/>; it's written after the fresh
/// <see cref="Mp3LogFile"/> below rather than before, so the log exists to be
/// hashed. Writes a
/// fresh <see cref="Mp3LogFile"/> for this run every time (unlike the FLAC
/// log, this one is genuinely regenerated content, not moved verbatim).
/// </summary>
public sealed class Mp3Packager
{
    private readonly LameEncoder _lameEncoder = new();

    /// <summary>Encodes one disc's FLAC files into the project's standard MP3 folder layout.</summary>
    /// <param name="options">The packaging options.</param>
    /// <param name="standardOutput">The stream to relay per-track progress and lame's own stdout into.</param>
    /// <param name="standardError">The stream to relay lame's stderr into.</param>
    /// <param name="cancellationToken">A token to cancel the encode.</param>
    /// <returns>Where everything ended up.</returns>
    /// <exception cref="ArgumentException"><paramref name="options"/>'s disc number is missing or out of range for a multi-disc release.</exception>
    /// <exception cref="DirectoryNotFoundException"><see cref="Mp3PackageOptions.SourceDirectory"/> doesn't exist.</exception>
    /// <exception cref="InvalidOperationException">The source directory has no <c>.flac</c> files, none of them match a track number for the target disc, or <c>lame</c> fails on a track.</exception>
    public async Task<Mp3PackageResult> PackageAsync(
        Mp3PackageOptions options,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (!Directory.Exists(options.SourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source directory not found: '{options.SourceDirectory}'.");
        }

        var releaseInfo = options.ReleaseInfo;
        var isMultiDisc = releaseInfo.Media.Count > 1;
        var discNumber = ReleaseFolderNaming.ResolveDiscNumber(releaseInfo, options.DiscNumber);
        var medium = releaseInfo.Media.Single(m => m.Position == discNumber);

        var containerDir = Path.Combine(options.DestinationParentDirectory, ReleaseFolderNaming.ContainerFolderName(releaseInfo, "mp3 v0"));
        var discDir = isMultiDisc ? Path.Combine(containerDir, ReleaseFolderNaming.DiscFolderName(discNumber)) : containerDir;
        Directory.CreateDirectory(discDir);

        var flacFiles = Directory.GetFiles(options.SourceDirectory, "*.flac");
        if (flacFiles.Length == 0)
        {
            throw new InvalidOperationException($"No .flac files found in '{options.SourceDirectory}'.");
        }

        var matches = TrackFileMatcher.Match(flacFiles, medium.Tracks);
        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"None of the .flac file(s) in '{options.SourceDirectory}' matched a track number for disc {discNumber} of '{releaseInfo.Title}'.");
        }

        var coverArtSourcePath = FindFlacCoverArt(options.SourceDirectory, isMultiDisc);

        var startTime = DateTimeOffset.Now;
        var trackLogEntries = await EncodeTracksAsync(releaseInfo, matches, medium.Tracks.Count, discDir, standardOutput, standardError, cancellationToken)
            .ConfigureAwait(false);
        var endTime = DateTimeOffset.Now;

        var copiedCoverArtPath = TryCopyCoverArt(coverArtSourcePath, containerDir);

        ReleaseInfoFile.Save(releaseInfo, Path.Combine(containerDir, "releaseinfo.json"));
        IdTextFile.Write(releaseInfo, Path.Combine(containerDir, "id.txt"));

        var logFilePath = Path.Combine(discDir, ReleaseFolderNaming.ReleaseDisplayName(releaseInfo) + ".log");
        Mp3LogFile.Write(
            new Mp3LogInfo(SystemInfo.GetUname(), SystemInfo.GetOsPrettyName(), SystemInfo.GetLameVersion(), startTime, endTime, trackLogEntries),
            logFilePath);

        // Written after the log, not before -- WriteChecksums includes .log files, so it
        // must run once the log actually exists on disk (see backlog-completed 003).
        ReleasePackageArtifacts.WriteChecksums(containerDir, ".mp3");
        ReleasePackageArtifacts.WritePlaylist(releaseInfo, containerDir, isMultiDisc, ".mp3");

        return new Mp3PackageResult(containerDir, discDir, matches.Count, logFilePath, copiedCoverArtPath);
    }

    /// <summary>Encodes every matched track to MP3 in <paramref name="discDir"/>, in track-number order.</summary>
    /// <param name="releaseInfo">The release being packaged.</param>
    /// <param name="matches">The tracks with a matching source FLAC file (see <see cref="TrackFileMatcher"/>), in track-number order.</param>
    /// <param name="totalTrackCount">The disc's full track count (for the <c>N/total</c> tag -- may exceed <paramref name="matches"/>' count on a degraded rip).</param>
    /// <param name="discDir">The destination disc directory.</param>
    /// <param name="standardOutput">The stream to relay per-track progress and lame's own stdout into.</param>
    /// <param name="standardError">The stream to relay lame's stderr into.</param>
    /// <param name="cancellationToken">A token to cancel the encode.</param>
    /// <returns>Each encoded track's captured <c>lame</c> output, in encode order, for the MP3 log.</returns>
    private async Task<List<Mp3TrackLogEntry>> EncodeTracksAsync(
        ReleaseInfo releaseInfo,
        IReadOnlyList<(TrackInfo Track, string FilePath)> matches,
        int totalTrackCount,
        string discDir,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken)
    {
        var year = ReleaseFolderNaming.ExtractYear(releaseInfo.Date);
        var yearOrNull = year == "0000" ? null : year;
        var trackLogEntries = new List<Mp3TrackLogEntry>();

        for (var i = 0; i < matches.Count; i++)
        {
            var (track, flacPath) = matches[i];
            await StreamLineWriter.WriteLineAsync(standardOutput, $"Track {i + 1} of {matches.Count}: {track.Title}", cancellationToken)
                .ConfigureAwait(false);

            var outputPath = Path.Combine(discDir, Path.GetFileNameWithoutExtension(flacPath) + ".mp3");
            var encodeOptions = new LameEncodeOptions(
                InputFlacPath: flacPath,
                OutputMp3Path: outputPath,
                Title: track.Title,
                Artist: track.Artist,
                Album: releaseInfo.Title,
                AlbumArtist: releaseInfo.Artist,
                Year: yearOrNull,
                TrackNumber: track.Number,
                TrackCount: totalTrackCount,
                Genre: releaseInfo.Discogs?.Genre);

            var result = await _lameEncoder.EncodeAsync(encodeOptions, standardOutput, standardError, cancellationToken)
                .ConfigureAwait(false);
            if (!result.Success)
            {
                throw new InvalidOperationException($"lame exited with code {result.ExitCode} encoding '{flacPath}'.");
            }

            trackLogEntries.Add(new Mp3TrackLogEntry(i + 1, matches.Count, track.Title, result.CapturedOutput));
        }

        return trackLogEntries;
    }

    /// <summary>Locates the FLAC folder's cover art, checking the disc folder and (for multi-disc releases) its container parent.</summary>
    /// <param name="sourceDirectory">The FLAC disc folder being encoded from.</param>
    /// <param name="isMultiDisc">Whether the release has more than one disc.</param>
    /// <returns>The cover art file's path, or <see langword="null"/> if none was found.</returns>
    private static string? FindFlacCoverArt(string sourceDirectory, bool isMultiDisc)
    {
        var found = Directory.EnumerateFiles(sourceDirectory, "cover.*").FirstOrDefault();
        if (found is not null)
        {
            return found;
        }

        if (!isMultiDisc)
        {
            return null;
        }

        var parent = Directory.GetParent(sourceDirectory)?.FullName;
        return parent is null ? null : Directory.EnumerateFiles(parent, "cover.*").FirstOrDefault();
    }

    /// <summary>Copies the FLAC folder's cover art into the MP3 container folder, if not already present there.</summary>
    /// <param name="coverArtSourcePath">The FLAC folder's cover art path, or <see langword="null"/> if none was found.</param>
    /// <param name="containerDir">The MP3 release's container folder.</param>
    /// <returns>Where the cover art was copied to, or <see langword="null"/> if there was none to copy.</returns>
    private static string? TryCopyCoverArt(string? coverArtSourcePath, string containerDir)
    {
        if (coverArtSourcePath is null)
        {
            return null;
        }

        var destination = Path.Combine(containerDir, "cover" + Path.GetExtension(coverArtSourcePath));
        if (!File.Exists(destination))
        {
            File.Copy(coverArtSourcePath, destination);
        }

        return destination;
    }
}
