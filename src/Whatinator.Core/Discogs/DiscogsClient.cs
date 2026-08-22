using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Whatinator.Core.Discogs;

namespace Whatinator.Core.Discogs;

/// <summary>A thin client for the subset of the Discogs API this project needs.</summary>
/// <remarks>
/// Barcode search works fully unauthenticated (confirmed against the live
/// API -- see <c>docs/plan/implementation/phase-004.md</c> § Research
/// findings), at a 25 requests/minute limit, which is trivially sufficient
/// for a personal tool doing at most one or two lookups per rip session. No
/// API token handling is implemented since none is required for this
/// project's usage.
/// </remarks>
public sealed class DiscogsClient : IDiscogsClient
{
    /// <summary>The base URL for the Discogs API.</summary>
    private const string BaseUrl = "https://api.discogs.com/";

    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="DiscogsClient"/> class.</summary>
    /// <param name="userAgent">
    /// The <c>User-Agent</c> header value sent with every request. Discogs,
    /// like MusicBrainz, expects a descriptive value identifying the
    /// application.
    /// </param>
    /// <param name="httpClient">
    /// The <see cref="HttpClient"/> to issue requests with -- owned by the
    /// caller (typically resolved from a shared <c>IHttpClientFactory</c>),
    /// not disposed by this class. Tests pass <c>new HttpClient(stubHandler)</c>
    /// instead of hitting the real network.
    /// </param>
    public DiscogsClient(string userAgent, HttpClient httpClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    /// <summary>Searches Discogs for releases matching a barcode.</summary>
    /// <param name="barcode">The barcode (UPC/EAN) to search for.</param>
    /// <returns>Every matching release Discogs returns, best guess first.</returns>
    /// <exception cref="DiscogsException">The request failed or the response couldn't be parsed.</exception>
    public async Task<IReadOnlyList<DiscogsInfo>> SearchByBarcodeAsync(string barcode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(barcode);

        var url = $"database/search?barcode={Uri.EscapeDataString(barcode)}&type=release";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<DiscogsSearchResponse>(url).ConfigureAwait(false)
                ?? throw new DiscogsException($"Discogs returned an empty response for '{url}'.");
            return response.Results.Select(ToDiscogsInfo).ToList();
        }
        catch (HttpRequestException ex)
        {
            throw new DiscogsException($"Discogs request failed for '{url}'.", ex);
        }
        catch (JsonException ex)
        {
            throw new DiscogsException($"Discogs response for '{url}' couldn't be parsed.", ex);
        }
    }

    /// <summary>Converts a wire-format search result into a <see cref="DiscogsInfo"/>.</summary>
    /// <param name="result">The wire-format search result.</param>
    /// <returns>The mapped release info.</returns>
    private static DiscogsInfo ToDiscogsInfo(DiscogsSearchResult result) => new(
        Id: result.Id.ToString(CultureInfo.InvariantCulture),
        Title: result.Title,
        Country: NullIfBlank(result.Country),
        Format: result.Format.Count > 0 ? string.Join(", ", result.Format) : null,
        Genre: result.Genre.Count > 0 ? string.Join(", ", result.Genre) : null,
        Style: result.Style.Count > 0 ? string.Join(", ", result.Style) : null,
        Label: result.Label.Count > 0 ? result.Label[0] : null,
        CatalogNumber: NullIfBlank(result.Catno),
        Url: result.Uri is null ? string.Empty : $"https://www.discogs.com{result.Uri}");

    /// <summary>Returns <see langword="null"/> for a blank string, otherwise the string unchanged.</summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="null"/> if <paramref name="value"/> is null or whitespace; otherwise <paramref name="value"/>.</returns>
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
