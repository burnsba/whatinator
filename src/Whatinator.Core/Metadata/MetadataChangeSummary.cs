namespace Whatinator.Core.Metadata;

/// <summary>
/// A pure comparison between a release folder's existing metadata and an
/// incoming replacement, computed by <see cref="MetadataUpdater.DetectChange"/>.
/// </summary>
/// <param name="OldArtist">The existing <see cref="ReleaseInfo.Artist"/>.</param>
/// <param name="NewArtist">The incoming <see cref="ReleaseInfo.Artist"/>.</param>
/// <param name="OldTitle">The existing <see cref="ReleaseInfo.Title"/>.</param>
/// <param name="NewTitle">The incoming <see cref="ReleaseInfo.Title"/>.</param>
/// <param name="OldYear">The 4-digit year extracted from the existing <see cref="ReleaseInfo.Date"/>.</param>
/// <param name="NewYear">The 4-digit year extracted from the incoming <see cref="ReleaseInfo.Date"/>.</param>
public sealed record MetadataChangeSummary(
    string OldArtist,
    string NewArtist,
    string OldTitle,
    string NewTitle,
    string OldYear,
    string NewYear)
{
    /// <summary>Whether the artist or title differs -- the trigger for <c>update-metadata</c>'s confirmation prompt.</summary>
    public bool ArtistOrTitleChanged =>
        !string.Equals(OldArtist, NewArtist, StringComparison.Ordinal) || !string.Equals(OldTitle, NewTitle, StringComparison.Ordinal);

    /// <summary>Whether the release year differs -- one of the triggers for a folder rename.</summary>
    public bool YearChanged => !string.Equals(OldYear, NewYear, StringComparison.Ordinal);
}
