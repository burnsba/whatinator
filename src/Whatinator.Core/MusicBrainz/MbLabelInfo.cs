using System.Text.Json.Serialization;

namespace Whatinator.Core.MusicBrainz;

/// <summary>One label/catalog-number pairing for a release.</summary>
internal sealed class MbLabelInfo
{
    /// <summary>The catalog number under this label.</summary>
    [JsonPropertyName("catalog-number")]
    public string? CatalogNumber { get; set; }

    /// <summary>The label itself.</summary>
    [JsonPropertyName("label")]
    public MbLabel? Label { get; set; }
}
