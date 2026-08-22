namespace Whatinator.Core.Toc;

/// <summary>
/// A minimal frame-accurate disc table of contents -- just enough to feed
/// <see cref="Whatinator.Core.AccurateRip.AccurateRipDiscId"/> and
/// <see cref="Whatinator.Core.AccurateRip.CddbDiscId"/>. Deliberately not the
/// same model as <see cref="Whatinator.Core.Metadata.MediumInfo"/>/
/// <see cref="Whatinator.Core.Metadata.TrackInfo"/> -- those are sourced from
/// MusicBrainz for tagging/<c>id.txt</c> and carry no frame offsets.
/// </summary>
/// <param name="Tracks">
/// Every track on the disc, audio and data alike, ordered by
/// <see cref="DiscTocTrack.TrackNumber"/>. Phase 013's <c>cdrdao</c> parser
/// populates this from a real disc.
/// </param>
/// <param name="CatalogNumber">
/// The disc's catalogue number (UPC/EAN), from the <c>.toc</c> file's
/// <c>CATALOG "value"</c> line -- <see langword="null"/> when the disc has
/// none. Trailing/optional so every pre-existing <c>new DiscToc(tracks)</c>
/// call site keeps compiling.
/// </param>
public sealed record DiscToc(IReadOnlyList<DiscTocTrack> Tracks, string? CatalogNumber = null)
{
    /// <summary>
    /// The absolute frame where the leadout begins -- one past the last
    /// track's <see cref="DiscTocTrack.EndFrame"/>, regardless of whether
    /// that last track is audio or data (a data track still determines
    /// where the leadout lands).
    /// </summary>
    public int LeadoutFrame => Tracks[^1].EndFrame + 1;
}
