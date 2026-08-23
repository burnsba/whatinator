using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Whatinator.Core.Metadata;

namespace Whatinator.Core.MusicBrainz;

/// <summary>A thin client for the subset of the MusicBrainz web service API this project needs.</summary>
public sealed class MusicBrainzClient : IMusicBrainzClient
{
    /// <summary>The base URL for the MusicBrainz web service.</summary>
    private const string BaseUrl = "https://musicbrainz.org/ws/2/";

    /// <summary>The maximum number of attempts (the first try plus retries) for a transient failure before giving up.</summary>
    private const int MaxAttempts = 10;

    /// <summary>The delay before the first retry; doubles on each subsequent retry, up to <see cref="MaxRetryDelay"/>.</summary>
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(1);

    /// <summary>The longest delay ever waited between retries (3 minutes, 10 seconds).</summary>
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly Action<int, int, TimeSpan, Exception>? _onRetry;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    /// <summary>Initializes a new instance of the <see cref="MusicBrainzClient"/> class.</summary>
    /// <param name="userAgent">
    /// The <c>User-Agent</c> header value sent with every request. MusicBrainz
    /// requires a descriptive value identifying the application and a contact
    /// method -- requests with a generic or missing User-Agent may be rate
    /// limited or blocked.
    /// </param>
    /// <param name="httpClient">
    /// The <see cref="HttpClient"/> to issue requests with -- owned by the
    /// caller (typically resolved from a shared <c>IHttpClientFactory</c>),
    /// not disposed by this class. Tests pass <c>new HttpClient(stubHandler)</c>
    /// instead of hitting the real network.
    /// </param>
    /// <param name="onRetry">
    /// Invoked just before each backoff wait for a transient failure (network
    /// error, request timeout, HTTP 429, or HTTP 5xx), with the attempt number
    /// just failed, <see cref="MaxAttempts"/>, the delay about to be waited,
    /// and the exception that triggered the retry. Lets the caller (the CLI)
    /// surface retry progress without this class doing any console I/O
    /// itself. <see langword="null"/> to retry silently.
    /// </param>
    public MusicBrainzClient(string userAgent, HttpClient httpClient, Action<int, int, TimeSpan, Exception>? onRetry = null)
        : this(userAgent, httpClient, onRetry, (delay, cancellationToken) => Task.Delay(delay, cancellationToken))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MusicBrainzClient"/> class,
    /// with the backoff delay replaced for tests -- used by
    /// <c>Whatinator.Core.Tests</c> to substitute an instant delay instead of
    /// actually waiting out the exponential backoff.
    /// </summary>
    /// <param name="userAgent">The <c>User-Agent</c> header value sent with every request.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to issue requests with.</param>
    /// <param name="onRetry">Invoked just before each backoff wait; <see langword="null"/> to retry silently.</param>
    /// <param name="delayAsync">Replaces the real <see cref="Task.Delay(TimeSpan, CancellationToken)"/> wait between retries.</param>
    internal MusicBrainzClient(string userAgent, HttpClient httpClient, Action<int, int, TimeSpan, Exception>? onRetry, Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userAgent);
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        _onRetry = onRetry;
        _delayAsync = delayAsync;
    }

    /// <summary>Looks up every release matching the given MusicBrainz disc ID.</summary>
    /// <param name="discId">The MusicBrainz disc ID, as returned by <see cref="LibDiscId.DiscReader"/>.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>Every matching release, without full track listings (see <see cref="GetReleaseAsync"/>).</returns>
    /// <exception cref="MusicBrainzException">The request failed or the response couldn't be parsed.</exception>
    public async Task<IReadOnlyList<ReleaseCandidate>> LookupByDiscIdAsync(string discId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(discId);

        var url = $"discid/{Uri.EscapeDataString(discId)}?fmt=json&inc=artist-credits+labels";
        var response = await GetAsync<MbDiscIdResponse>(url, cancellationToken).ConfigureAwait(false);

        return response.Releases.Select(ToCandidate).ToList();
    }

    /// <summary>Fetches the full metadata (including every disc's full track listing) for a release.</summary>
    /// <param name="releaseId">The MusicBrainz release MBID.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The full release metadata.</returns>
    /// <exception cref="MusicBrainzException">The request failed or the response couldn't be parsed.</exception>
    public async Task<ReleaseInfo> GetReleaseAsync(string releaseId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(releaseId);

        var url = $"release/{Uri.EscapeDataString(releaseId)}?fmt=json&inc=recordings+artist-credits+labels";
        var release = await GetAsync<MbRelease>(url, cancellationToken).ConfigureAwait(false);

        return ToReleaseInfo(release);
    }

    /// <summary>
    /// Issues a GET request and deserializes the JSON response, translating
    /// failures into <see cref="MusicBrainzException"/>. Transient failures
    /// (network error, request timeout, HTTP 429, or HTTP 5xx) are retried
    /// with exponential backoff, up to <see cref="MaxAttempts"/> attempts
    /// total, capped at <see cref="MaxRetryDelay"/> between attempts. A
    /// client-side request timeout surfaces as <see cref="TaskCanceledException"/>
    /// rather than <see cref="HttpRequestException"/> in .NET 5+, so it's
    /// handled as its own transient-failure branch rather than folding into
    /// <see cref="IsTransientFailure"/>. Non-transient failures (e.g. HTTP
    /// 404) are not retried. <paramref name="cancellationToken"/> cancellation
    /// is never treated as transient -- it propagates immediately, unwrapped,
    /// distinguished from a timeout by comparing
    /// <see cref="OperationCanceledException.CancellationToken"/> against the
    /// caller's token.
    /// </summary>
    /// <typeparam name="T">The expected response shape.</typeparam>
    /// <param name="relativeUrl">The URL, relative to <see cref="BaseUrl"/>.</param>
    /// <param name="cancellationToken">A token to cancel the request and any pending retry backoff.</param>
    /// <returns>The deserialized response.</returns>
    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var result = await _httpClient.GetFromJsonAsync<T>(relativeUrl, cancellationToken).ConfigureAwait(false);
                return result ?? throw new MusicBrainzException($"MusicBrainz returned an empty response for '{relativeUrl}'.");
            }
            catch (HttpRequestException ex) when (attempt < MaxAttempts && IsTransientFailure(ex))
            {
                var delay = ComputeRetryDelay(attempt);
                _onRetry?.Invoke(attempt, MaxAttempts, delay, ex);
                await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new MusicBrainzException($"MusicBrainz request failed for '{relativeUrl}': {ex.Message}", ex);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken && attempt < MaxAttempts)
            {
                var delay = ComputeRetryDelay(attempt);
                _onRetry?.Invoke(attempt, MaxAttempts, delay, ex);
                await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
            {
                throw new MusicBrainzException($"MusicBrainz request timed out for '{relativeUrl}': {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new MusicBrainzException($"MusicBrainz response for '{relativeUrl}' couldn't be parsed: {ex.Message}", ex);
            }
            catch (NotSupportedException ex)
            {
                throw new MusicBrainzException($"MusicBrainz returned an unexpected response for '{relativeUrl}': {ex.Message}", ex);
            }
        }
    }

    /// <summary>Determines whether an <see cref="HttpRequestException"/> represents a failure worth retrying.</summary>
    /// <param name="ex">The exception to inspect.</param>
    /// <returns>
    /// <see langword="true"/> for a connection-level failure (no status code),
    /// HTTP 408, HTTP 429, or any HTTP 5xx; <see langword="false"/> for any
    /// other HTTP status, which won't succeed no matter how many times it's
    /// retried.
    /// </returns>
    private static bool IsTransientFailure(HttpRequestException ex) =>
        ex.StatusCode is null
        || ex.StatusCode == HttpStatusCode.RequestTimeout
        || ex.StatusCode == HttpStatusCode.TooManyRequests
        || (int)ex.StatusCode >= 500;

    /// <summary>Computes the exponential backoff delay before the retry following the given attempt.</summary>
    /// <param name="attempt">The attempt number (1-based) that just failed.</param>
    /// <returns><see cref="BaseRetryDelay"/> doubled once per prior attempt, capped at <see cref="MaxRetryDelay"/>.</returns>
    private static TimeSpan ComputeRetryDelay(int attempt)
    {
        var seconds = Math.Min(MaxRetryDelay.TotalSeconds, BaseRetryDelay.TotalSeconds * Math.Pow(2, attempt - 1));
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Converts a wire-format release into a lightweight <see cref="ReleaseCandidate"/>.</summary>
    /// <param name="release">The wire-format release.</param>
    /// <returns>The mapped candidate.</returns>
    private static ReleaseCandidate ToCandidate(MbRelease release) => new(
        MusicBrainzReleaseId: release.Id,
        Artist: JoinArtistCredit(release.ArtistCredit),
        Title: release.Title,
        Date: release.Date,
        Country: release.Country,
        Barcode: release.Barcode,
        CatalogNumber: FirstNonBlank(release.LabelInfo.Select(l => l.CatalogNumber)));

    /// <summary>Converts a wire-format release into the full <see cref="ReleaseInfo"/> persisted to <c>releaseinfo.json</c>.</summary>
    /// <param name="release">The wire-format release.</param>
    /// <returns>The mapped release info.</returns>
    private static ReleaseInfo ToReleaseInfo(MbRelease release) => new(
        MusicBrainzReleaseId: release.Id,
        MusicBrainzUrl: $"https://musicbrainz.org/release/{release.Id}",
        Artist: JoinArtistCredit(release.ArtistCredit),
        Title: release.Title,
        Date: release.Date,
        Country: release.Country,
        Barcode: release.Barcode,
        Label: FirstNonBlank(release.LabelInfo.Select(l => l.Label?.Name)),
        CatalogNumber: FirstNonBlank(release.LabelInfo.Select(l => l.CatalogNumber)),
        Media: release.Media.Select(ToMedium).ToList());

    /// <summary>Converts a wire-format medium into a <see cref="MediumInfo"/>.</summary>
    /// <param name="medium">The wire-format medium.</param>
    /// <returns>The mapped medium.</returns>
    private static MediumInfo ToMedium(MbMedium medium) => new(
        Position: medium.Position,
        Subtitle: string.IsNullOrWhiteSpace(medium.Title) ? null : medium.Title,
        Tracks: medium.Tracks.Select(ToTrack).ToList());

    /// <summary>Converts a wire-format track into a <see cref="TrackInfo"/>.</summary>
    /// <param name="track">The wire-format track.</param>
    /// <returns>The mapped track.</returns>
    private static TrackInfo ToTrack(MbTrack track) => new(
        Number: track.Position,
        Title: track.Title,
        Artist: JoinArtistCredit(track.ArtistCredit),
        Duration: TimeSpan.FromMilliseconds(track.Length ?? 0));

    /// <summary>Joins a MusicBrainz artist credit's name/joinphrase pairs into a single display string.</summary>
    /// <param name="credits">The artist credit entries, in order.</param>
    /// <returns>The joined artist credit string.</returns>
    private static string JoinArtistCredit(IEnumerable<MbArtistCredit> credits) =>
        string.Concat(credits.Select(c => c.Name + c.Joinphrase));

    /// <summary>Returns the first non-null, non-whitespace value in <paramref name="values"/>, or <see langword="null"/> if none.</summary>
    /// <param name="values">The candidate values, in preference order.</param>
    /// <returns>The first usable value, or <see langword="null"/>.</returns>
    private static string? FirstNonBlank(IEnumerable<string?> values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
