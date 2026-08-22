using System.Text.Json.Serialization;

namespace Whatinator.Core.MusicBrainz;

/// <summary>
/// Wire-format MusicBrainz release. Shared by the discid endpoint (where
/// <see cref="Media"/> is filtered to just the matching disc) and the
/// release endpoint (where <see cref="Media"/> is the full release).
/// </summary>
internal sealed class MbRelease
{
    /// <summary>The MusicBrainz release MBID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The release (album) title.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>The release date, possibly partial (e.g. just a year).</summary>
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    /// <summary>The release country (ISO 3166-1 alpha-2).</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>The release barcode.</summary>
    [JsonPropertyName("barcode")]
    public string? Barcode { get; set; }

    /// <summary>The release's artist credit, as a sequence of name/joinphrase pairs.</summary>
    [JsonPropertyName("artist-credit")]
    public List<MbArtistCredit> ArtistCredit { get; set; } = new();

    /// <summary>The release's label(s) and catalog number(s).</summary>
    [JsonPropertyName("label-info")]
    public List<MbLabelInfo> LabelInfo { get; set; } = new();

    /// <summary>The release's media (discs).</summary>
    [JsonPropertyName("media")]
    public List<MbMedium> Media { get; set; } = new();
}
