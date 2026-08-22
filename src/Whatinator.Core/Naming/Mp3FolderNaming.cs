using Whatinator.Core.Metadata;

namespace Whatinator.Core.Naming;

/// <summary>Computes the MP3-specific container folder name for a release. See <see cref="ReleaseFolderNaming"/> for the shared, format-agnostic pieces.</summary>
public static class Mp3FolderNaming
{
    /// <summary>
    /// Builds the container folder name: <c>"{Artist} - {Title} [mp3 v0 {Year}]"</c>,
    /// with a leading <c>"The "</c> in the artist reordered to sort
    /// alongside other artists (see <see cref="ReleaseFolderNaming.SortArtist"/>).
    /// </summary>
    /// <param name="releaseInfo">The release to name a folder for.</param>
    /// <returns>The sanitized folder name (not a full path).</returns>
    public static string ContainerFolderName(ReleaseInfo releaseInfo)
    {
        ArgumentNullException.ThrowIfNull(releaseInfo);

        var year = ReleaseFolderNaming.ExtractYear(releaseInfo.Date);
        var sortArtist = ReleaseFolderNaming.SortArtist(releaseInfo.Artist);
        return FileNameSanitizer.Sanitize($"{sortArtist} - {releaseInfo.Title} [mp3 v0 {year}]");
    }
}
