using System.Text.Json.Serialization;

namespace Whatinator.Core.Discogs;

/// <summary>One release in a Discogs search response.</summary>
internal sealed class DiscogsSearchResult
{
    /// <summary>The Discogs release ID.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>The listing title, typically <c>"Artist - Album"</c>.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>The release country.</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>The format (e.g. <c>["CD", "Album"]</c>).</summary>
    [JsonPropertyName("format")]
    public List<string> Format { get; set; } = new();

    /// <summary>The genre(s) (e.g. <c>["Electronic"]</c>).</summary>
    [JsonPropertyName("genre")]
    public List<string> Genre { get; set; } = new();

    /// <summary>The style(s) (e.g. <c>["Synth-pop"]</c>).</summary>
    [JsonPropertyName("style")]
    public List<string> Style { get; set; } = new();

    /// <summary>The label(s) credited on this release.</summary>
    [JsonPropertyName("label")]
    public List<string> Label { get; set; } = new();

    /// <summary>The catalog number.</summary>
    [JsonPropertyName("catno")]
    public string? Catno { get; set; }

    /// <summary>The web page path for this release (relative -- needs the Discogs origin prepended).</summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
}
