using System.Text.Json.Serialization;

namespace Whatinator.Core.MusicBrainz;

/// <summary>One disc within a release.</summary>
internal sealed class MbMedium
{
    /// <summary>The disc number within the release (1 for a single-disc release).</summary>
    [JsonPropertyName("position")]
    public int Position { get; set; }

    /// <summary>The disc's own title, or an empty string if it has none.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>Every track on this disc, in order.</summary>
    [JsonPropertyName("tracks")]
    public List<MbTrack> Tracks { get; set; } = new();
}
