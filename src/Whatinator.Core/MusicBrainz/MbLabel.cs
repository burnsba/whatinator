using System.Text.Json.Serialization;

namespace Whatinator.Core.MusicBrainz;

/// <summary>A record label.</summary>
internal sealed class MbLabel
{
    /// <summary>The label's name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
