using System.Text.Json.Serialization;

namespace Whatinator.Core.Discogs;

/// <summary>Wire-format response from the <c>/database/search</c> endpoint.</summary>
internal sealed class DiscogsSearchResponse
{
    /// <summary>Every release matching the search query.</summary>
    [JsonPropertyName("results")]
    public List<DiscogsSearchResult> Results { get; set; } = new();
}
