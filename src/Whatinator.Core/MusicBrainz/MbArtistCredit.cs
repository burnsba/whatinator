using System.Text.Json.Serialization;

namespace Whatinator.Core.MusicBrainz;

/// <summary>
/// One entry in a MusicBrainz artist credit -- a name plus the text to join
/// it to the next entry with (e.g. <c>"Artist A"</c> + <c>" feat. "</c> +
/// <c>"Artist B"</c>).
/// </summary>
internal sealed class MbArtistCredit
{
    /// <summary>The artist's credited name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>The text joining this entry to the next one, or an empty string if this is the last entry.</summary>
    [JsonPropertyName("joinphrase")]
    public string Joinphrase { get; set; } = string.Empty;
}
