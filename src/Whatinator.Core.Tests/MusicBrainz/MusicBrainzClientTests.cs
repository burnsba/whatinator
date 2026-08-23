using System.Net;
using Whatinator.Core.MusicBrainz;

namespace Whatinator.Core.Tests;

public class MusicBrainzClientTests
{
    [Fact]
    public async Task LookupByDiscIdAsync_ReturnsEmptyList_WhenNoReleasesMatch()
    {
        var client = CreateClient(HttpStatusCode.OK, """{ "releases": [] }""");

        var candidates = await client.LookupByDiscIdAsync("some-disc-id");

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_MapsCandidateFields()
    {
        const string json = """
            {
              "releases": [
                {
                  "id": "release-1",
                  "title": "Diva",
                  "date": "1992-05-12",
                  "country": "US",
                  "barcode": "078221870429",
                  "artist-credit": [ { "name": "Annie Lennox", "joinphrase": "" } ],
                  "label-info": [ { "catalog-number": "07822-18704-2", "label": { "name": "Arista" } } ],
                  "media": []
                }
              ]
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var candidates = await client.LookupByDiscIdAsync("some-disc-id");

        var candidate = Assert.Single(candidates);
        Assert.Equal("release-1", candidate.MusicBrainzReleaseId);
        Assert.Equal("Annie Lennox", candidate.Artist);
        Assert.Equal("Diva", candidate.Title);
        Assert.Equal("1992-05-12", candidate.Date);
        Assert.Equal("US", candidate.Country);
        Assert.Equal("078221870429", candidate.Barcode);
        Assert.Equal("07822-18704-2", candidate.CatalogNumber);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_JoinsMultiArtistCredit()
    {
        const string json = """
            {
              "releases": [
                {
                  "id": "release-1",
                  "title": "Collab Track",
                  "artist-credit": [
                    { "name": "Artist A", "joinphrase": " feat. " },
                    { "name": "Artist B", "joinphrase": "" }
                  ],
                  "media": []
                }
              ]
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var candidates = await client.LookupByDiscIdAsync("some-disc-id");

        Assert.Equal("Artist A feat. Artist B", Assert.Single(candidates).Artist);
    }

    [Fact]
    public async Task GetReleaseAsync_MapsAllMediaAndTracks_IncludingMultiDisc()
    {
        const string json = """
            {
              "id": "release-1",
              "title": "Double Album",
              "date": "2001-01-01",
              "country": "US",
              "artist-credit": [ { "name": "Some Artist", "joinphrase": "" } ],
              "label-info": [ { "catalog-number": "CAT-001", "label": { "name": "Some Label" } } ],
              "media": [
                {
                  "position": 1,
                  "title": "",
                  "tracks": [
                    { "position": 1, "title": "Disc 1 Track 1", "length": 180000, "artist-credit": [ { "name": "Some Artist", "joinphrase": "" } ] }
                  ]
                },
                {
                  "position": 2,
                  "title": "The Bonus Disc",
                  "tracks": [
                    { "position": 1, "title": "Disc 2 Track 1", "length": 240000, "artist-credit": [ { "name": "Some Artist", "joinphrase": "" } ] }
                  ]
                }
              ]
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var release = await client.GetReleaseAsync("release-1");

        Assert.Equal("https://musicbrainz.org/release/release-1", release.MusicBrainzUrl);
        Assert.Equal("Some Label", release.Label);
        Assert.Equal("CAT-001", release.CatalogNumber);
        Assert.Equal(2, release.Media.Count);
        Assert.Equal(1, release.Media[0].Position);
        Assert.Null(release.Media[0].Subtitle);
        Assert.Equal("Disc 1 Track 1", release.Media[0].Tracks[0].Title);
        Assert.Equal(TimeSpan.FromMilliseconds(180000), release.Media[0].Tracks[0].Duration);
        Assert.Equal(2, release.Media[1].Position);
        Assert.Equal("The Bonus Disc", release.Media[1].Subtitle);
        Assert.Equal("Disc 2 Track 1", release.Media[1].Tracks[0].Title);
        Assert.Equal(TimeSpan.FromMilliseconds(240000), release.Media[1].Tracks[0].Duration);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_ThrowsMusicBrainzException_OnMalformedJson()
    {
        var client = CreateClient(HttpStatusCode.OK, "{ not valid json");

        await Assert.ThrowsAsync<MusicBrainzException>(() => client.LookupByDiscIdAsync("some-disc-id"));
    }

    [Fact]
    public async Task LookupByDiscIdAsync_DoesNotRetry_OnNonTransientHttpError()
    {
        var callCount = 0;
        var client = CreateClient(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await Assert.ThrowsAsync<MusicBrainzException>(() => client.LookupByDiscIdAsync("some-disc-id"));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_RetriesTransientHttpError_ThenSucceeds()
    {
        var callCount = 0;
        var delays = new List<TimeSpan>();
        var client = CreateClient(
            _ =>
            {
                callCount++;
                return callCount < 3
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{ "releases": [] }""") };
            },
            onRetry: (attempt, maxAttempts, delay, ex) => delays.Add(delay));

        var candidates = await client.LookupByDiscIdAsync("some-disc-id");

        Assert.Empty(candidates);
        Assert.Equal(3, callCount);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)], delays);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_GivesUpAfterTenAttempts_OnPersistentTransientError()
    {
        var callCount = 0;
        var client = CreateClient(_ =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        await Assert.ThrowsAsync<MusicBrainzException>(() => client.LookupByDiscIdAsync("some-disc-id"));

        Assert.Equal(10, callCount);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_CapsRetryDelayAtThreeMinutesTen()
    {
        var callCount = 0;
        var delays = new List<TimeSpan>();
        var client = CreateClient(
            _ =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            },
            onRetry: (attempt, maxAttempts, delay, ex) => delays.Add(delay));

        await Assert.ThrowsAsync<MusicBrainzException>(() => client.LookupByDiscIdAsync("some-disc-id"));

        Assert.All(delays, delay => Assert.True(delay <= TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(10), delays[^1]);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_RetriesTimeout_ThenWrapsAfterExhaustingAttempts()
    {
        var callCount = 0;
        var client = CreateClient(_ =>
        {
            callCount++;
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.", new TimeoutException(), new CancellationToken(canceled: true));
        });

        await Assert.ThrowsAsync<MusicBrainzException>(() => client.LookupByDiscIdAsync("some-disc-id"));

        Assert.Equal(10, callCount);
    }

    [Fact]
    public async Task LookupByDiscIdAsync_ThrowsMusicBrainzException_OnUnexpectedContentType()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>MusicBrainz is down for maintenance</html>", System.Text.Encoding.UTF8, "text/html"),
        });
        var client = new MusicBrainzClient(
            "whatinator-tests/1.0 ( test@example.com )",
            new HttpClient(handler),
            onRetry: null,
            delayAsync: (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<MusicBrainzException>(() => client.LookupByDiscIdAsync("some-disc-id"));
    }

    [Fact]
    public async Task LookupByDiscIdAsync_CancellingCallerToken_AbortsBackoffDelay_InsteadOfWaitingItOut()
    {
        // Uses the real (non-instant) delay -- the public constructor's
        // Task.Delay(delay, cancellationToken) -- to prove cancellation
        // actually interrupts the wait rather than merely being ignored by
        // a test double. A transient 503 triggers the retry path, whose
        // first backoff is 1 second (BaseRetryDelay); an already-cancelled
        // token must abort that wait almost immediately.
        var client = new MusicBrainzClient(
            "whatinator-tests/1.0 ( test@example.com )",
            new HttpClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable, string.Empty)));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // The caller's own cancellation must never be treated as a transient
        // timeout to retry -- it should propagate immediately as some flavor
        // of OperationCanceledException, not get wrapped in MusicBrainzException.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.LookupByDiscIdAsync("some-disc-id", cts.Token));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Expected the cancelled backoff to abort promptly, took {stopwatch.Elapsed}.");
    }

    private static MusicBrainzClient CreateClient(HttpStatusCode statusCode, string responseBody) =>
        new(
            "whatinator-tests/1.0 ( test@example.com )",
            new HttpClient(new StubHttpMessageHandler(statusCode, responseBody)),
            onRetry: null,
            delayAsync: (_, _) => Task.CompletedTask);

    private static MusicBrainzClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder, Action<int, int, TimeSpan, Exception>? onRetry = null) =>
        new(
            "whatinator-tests/1.0 ( test@example.com )",
            new HttpClient(new StubHttpMessageHandler(responder)),
            onRetry,
            delayAsync: (_, _) => Task.CompletedTask);
}
