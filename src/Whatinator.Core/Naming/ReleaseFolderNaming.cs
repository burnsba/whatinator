using Whatinator.Core.Metadata;

namespace Whatinator.Core.Naming;

/// <summary>
/// Shared building blocks for release output folder/file names, used by
/// both FLAC and MP3 packaging.
/// </summary>
public static class ReleaseFolderNaming
{
    /// <summary>Builds a disc subfolder name: <c>"cd{N}"</c>.</summary>
    /// <param name="discNumber">The 1-based disc number.</param>
    /// <returns>The subfolder name.</returns>
    public static string DiscFolderName(int discNumber) => $"cd{discNumber}";

    /// <summary>Builds a plain <c>"{Artist} - {Title}"</c> display name (no folder-specific suffix), sanitized.</summary>
    /// <param name="releaseInfo">The release to name.</param>
    /// <returns>The sanitized display name.</returns>
    public static string ReleaseDisplayName(ReleaseInfo releaseInfo)
    {
        ArgumentNullException.ThrowIfNull(releaseInfo);

        return FileNameSanitizer.Sanitize($"{releaseInfo.Artist} - {releaseInfo.Title}");
    }

    /// <summary>
    /// Reorders a leading <c>"The "</c> to the end, comma-separated, so the
    /// artist sorts alongside other artists by their main name -- e.g.
    /// <c>"The Sugarcubes"</c> becomes <c>"Sugarcubes, The"</c>. Used only
    /// for container folder names (<see cref="ContainerFolderName"/>);
    /// <see cref="ReleaseInfo.Artist"/> itself, tags, <c>id.txt</c>, and the
    /// <c>.m3u</c> name (see <see cref="ReleaseDisplayName"/>) all keep the
    /// original word order.
    /// </summary>
    /// <param name="artist">The artist name to reorder.</param>
    /// <returns>The sort-friendly artist name, or <paramref name="artist"/> unchanged if it doesn't start with "The ".</returns>
    public static string SortArtist(string artist)
    {
        ArgumentNullException.ThrowIfNull(artist);

        const string Prefix = "The ";
        if (!artist.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return artist;
        }

        var article = artist[..3];
        var rest = artist[Prefix.Length..];
        return string.IsNullOrWhiteSpace(rest) ? artist : $"{rest}, {article}";
    }

    /// <summary>Extracts the leading 4-digit year from a MusicBrainz date string.</summary>
    /// <param name="date">The raw date string (full date, year-month, or year), or <see langword="null"/>.</param>
    /// <returns>The 4-digit year, or <c>"0000"</c> if none could be parsed.</returns>
    public static string ExtractYear(string? date)
    {
        if (!string.IsNullOrWhiteSpace(date) && date.Length >= 4 && int.TryParse(date.AsSpan(0, 4), out _))
        {
            return date[..4];
        }

        return "0000";
    }

    /// <summary>
    /// Builds the container folder name: <c>"{Artist} - {Title} [{formatTag} {Year}]"</c>,
    /// with a leading <c>"The "</c> in the artist reordered to sort alongside
    /// other artists (see <see cref="SortArtist"/>). The format tag is the
    /// caller's (the packager's) concern -- current callers pass
    /// <c>"flac"</c> or <c>"mp3 v0"</c>.
    /// </summary>
    /// <param name="releaseInfo">The release to name a folder for.</param>
    /// <param name="formatTag">The format tag to embed, e.g. <c>"flac"</c> or <c>"mp3 v0"</c>.</param>
    /// <returns>The sanitized folder name (not a full path).</returns>
    public static string ContainerFolderName(ReleaseInfo releaseInfo, string formatTag)
    {
        ArgumentNullException.ThrowIfNull(releaseInfo);
        ArgumentNullException.ThrowIfNull(formatTag);

        var year = ExtractYear(releaseInfo.Date);
        var sortArtist = SortArtist(releaseInfo.Artist);
        return FileNameSanitizer.Sanitize($"{sortArtist} - {releaseInfo.Title} [{formatTag} {year}]");
    }

    /// <summary>Resolves and validates the disc number a packaging/rip call is for.</summary>
    /// <param name="releaseInfo">The release being packaged or ripped.</param>
    /// <param name="requested">The caller-supplied disc number, if any.</param>
    /// <returns>The validated 1-based disc number.</returns>
    /// <exception cref="ArgumentException"><paramref name="requested"/> is missing or out of range for a multi-disc release.</exception>
    public static int ResolveDiscNumber(ReleaseInfo releaseInfo, int? requested)
    {
        ArgumentNullException.ThrowIfNull(releaseInfo);

        if (releaseInfo.Media.Count == 1)
        {
            return requested ?? 1;
        }

        if (requested is null)
        {
            throw new ArgumentException(
                $"'{releaseInfo.Title}' has {releaseInfo.Media.Count} discs; a disc number is required.");
        }

        if (requested < 1 || requested > releaseInfo.Media.Count)
        {
            throw new ArgumentException(
                $"Disc {requested} is out of range for '{releaseInfo.Title}' ({releaseInfo.Media.Count} discs).");
        }

        return requested.Value;
    }
}
