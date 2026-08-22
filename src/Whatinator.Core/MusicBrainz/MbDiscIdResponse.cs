using System.Text.Json.Serialization;

namespace Whatinator.Core.MusicBrainz;

/// <summary>Wire-format response from the <c>/ws/2/discid/{id}</c> endpoint.</summary>
internal sealed class MbDiscIdResponse
{
    /// <summary>Every release matching the queried disc ID.</summary>
    [JsonPropertyName("releases")]
    public List<MbRelease> Releases { get; set; } = new();
}
