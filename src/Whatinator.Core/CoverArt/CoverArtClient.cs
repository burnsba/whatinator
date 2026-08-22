using System.Net.Http.Headers;

namespace Whatinator.Core.CoverArt;

/// <summary>A thin client for the MusicBrainz Cover Art Archive.</summary>
/// <remarks>
/// Best-effort by design: every failure mode (no art, network error,
/// unexpected content type) is treated the same -- <see langword="null"/>,
/// never an exception -- per <c>init.md</c>'s "shouldn't be a blocking
/// issue" framing already applied to Discogs. Confirmed live:
/// <c>GET /release/{mbid}/front</c> redirects straight to the image on
/// success and returns a plain 404 when none exists.
/// </remarks>
public sealed class CoverArtClient : ICoverArtClient
{
    /// <summary>The base URL for the Cover Art Archive.</summary>
    private const string BaseUrl = "https://coverartarchive.org/";

    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="CoverArtClient"/> class.</summary>
    /// <param name="userAgent">The <c>User-Agent</c> header value sent with every request.</param>
    /// <param name="httpClient">
    /// The <see cref="HttpClient"/> to issue requests with -- owned by the
    /// caller (typically resolved from a shared <c>IHttpClientFactory</c>),
    /// not disposed by this class. Tests pass <c>new HttpClient(stubHandler)</c>
    /// instead of hitting the real network.
    /// </param>
    public CoverArtClient(string userAgent, HttpClient httpClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
    }

    /// <summary>Attempts to download a release's front cover image.</summary>
    /// <param name="musicBrainzReleaseId">The MusicBrainz release MBID.</param>
    /// <returns>The downloaded image, or <see langword="null"/> if none is available or the request failed.</returns>
    public async Task<CoverArtResult?> TryDownloadFrontCoverAsync(string musicBrainzReleaseId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(musicBrainzReleaseId);

        try
        {
            var url = $"release/{Uri.EscapeDataString(musicBrainzReleaseId)}/front";
            using var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var extension = ExtensionFor(response.Content.Headers.ContentType);
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return new CoverArtResult(bytes, extension);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>Infers a file extension from a response's content type, defaulting to <c>.jpg</c>.</summary>
    /// <param name="contentType">The response's <c>Content-Type</c> header, if any.</param>
    /// <returns>A file extension including the leading dot.</returns>
    private static string ExtensionFor(MediaTypeHeaderValue? contentType) => contentType?.MediaType switch
    {
        "image/png" => ".png",
        "image/gif" => ".gif",
        _ => ".jpg",
    };
}
