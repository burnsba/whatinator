using System.Text.Json.Serialization;

namespace Whatinator.Core.Discogs;

/// <summary>Wire-format response from the <c>/releases/{id}</c> endpoint.</summary>
/// <remarks>
/// A different shape from <see cref="DiscogsSearchResponse"/>'s per-result
/// entries (<see cref="DiscogsSearchResult"/>): formats and labels are
/// objects here rather than flat strings, and <see cref="Uri"/> is already the
/// full absolute web page URL rather than a path needing the origin
/// prepended. <see cref="DiscogsClient"/> maps across that difference.
/// </remarks>
internal sealed class DiscogsReleaseResponse
{
    /// <summary>The Discogs release ID.</summary>
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>The release title, typically <c>"Artist - Album"</c>.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>The release country.</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>The genre(s) (e.g. <c>["Electronic"]</c>).</summary>
    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();

    /// <summary>The style(s) (e.g. <c>["Synth-pop"]</c>).</summary>
    [JsonPropertyName("styles")]
    public List<string> Styles { get; set; } = new();

    /// <summary>The release's physical formats (e.g. CD, with descriptions like "Album").</summary>
    [JsonPropertyName("formats")]
    public List<DiscogsReleaseFormat> Formats { get; set; } = new();

    /// <summary>The label(s) credited on this release, each with its own catalog number.</summary>
    [JsonPropertyName("labels")]
    public List<DiscogsReleaseLabel> Labels { get; set; } = new();

    /// <summary>The full, absolute Discogs web page URL for this release.</summary>
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }
}

/// <summary>One entry of a Discogs release's <c>formats</c> array.</summary>
internal sealed class DiscogsReleaseFormat
{
    /// <summary>The format name (e.g. <c>"CD"</c>).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>Additional descriptions (e.g. <c>["Album"]</c>).</summary>
    [JsonPropertyName("descriptions")]
    public List<string> Descriptions { get; set; } = new();
}

/// <summary>One entry of a Discogs release's <c>labels</c> array.</summary>
internal sealed class DiscogsReleaseLabel
{
    /// <summary>The label name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>The catalog number this label assigned to the release.</summary>
    [JsonPropertyName("catno")]
    public string? Catno { get; set; }
}
