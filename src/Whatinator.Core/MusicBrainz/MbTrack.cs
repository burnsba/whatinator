using System.Text.Json.Serialization;

namespace Whatinator.Core.MusicBrainz;

/// <summary>One track within a disc.</summary>
internal sealed class MbTrack
{
    /// <summary>The track number (1-based) within its disc.</summary>
    [JsonPropertyName("position")]
    public int Position { get; set; }

    /// <summary>The track title.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>The track length in milliseconds, or <see langword="null"/> if unknown.</summary>
    [JsonPropertyName("length")]
    public int? Length { get; set; }

    /// <summary>The track's artist credit, as a sequence of name/joinphrase pairs.</summary>
    [JsonPropertyName("artist-credit")]
    public List<MbArtistCredit> ArtistCredit { get; set; } = new();
}
