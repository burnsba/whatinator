using System.Net;
using Whatinator.Core.Discogs;

namespace Whatinator.Core.Tests;

public class DiscogsClientTests
{
    [Fact]
    public async Task SearchByBarcodeAsync_ReturnsEmptyList_WhenNoResults()
    {
        var client = CreateClient(HttpStatusCode.OK, """{ "results": [] }""");

        var candidates = await client.SearchByBarcodeAsync("078221870429");

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_MapsFields_JoiningArraysAndTakingFirstLabel()
    {
        const string json = """
            {
              "results": [
                {
                  "id": 139878,
                  "title": "Annie Lennox - Diva",
                  "country": "US",
                  "format": [ "CD", "Album", "Stereo" ],
                  "genre": [ "Electronic" ],
                  "style": [ "Synth-pop", "Ballad" ],
                  "label": [ "Arista", "BMG" ],
                  "catno": "07822-18704-2",
                  "uri": "/release/139878-Annie-Lennox-Diva"
                }
              ]
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var candidates = await client.SearchByBarcodeAsync("078221870429");

        var candidate = Assert.Single(candidates);
        Assert.Equal("139878", candidate.Id);
        Assert.Equal("Annie Lennox - Diva", candidate.Title);
        Assert.Equal("US", candidate.Country);
        Assert.Equal("CD, Album, Stereo", candidate.Format);
        Assert.Equal("Electronic", candidate.Genre);
        Assert.Equal("Synth-pop, Ballad", candidate.Style);
        Assert.Equal("Arista", candidate.Label);
        Assert.Equal("07822-18704-2", candidate.CatalogNumber);
        Assert.Equal("https://www.discogs.com/release/139878-Annie-Lennox-Diva", candidate.Url);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_MapsMissingFieldsToNull()
    {
        const string json = """
            {
              "results": [
                { "id": 1, "title": "Some Release" }
              ]
            }
            """;
        var client = CreateClient(HttpStatusCode.OK, json);

        var candidate = Assert.Single(await client.SearchByBarcodeAsync("000000000000"));

        Assert.Null(candidate.Country);
        Assert.Null(candidate.Format);
        Assert.Null(candidate.Genre);
        Assert.Null(candidate.Style);
        Assert.Null(candidate.Label);
        Assert.Null(candidate.CatalogNumber);
        Assert.Equal(string.Empty, candidate.Url);
    }

    [Fact]
    public async Task SearchByBarcodeAsync_ThrowsDiscogsException_OnHttpError()
    {
        var client = CreateClient(HttpStatusCode.InternalServerError, "oops");

        await Assert.ThrowsAsync<DiscogsException>(() => client.SearchByBarcodeAsync("078221870429"));
    }

    [Fact]
    public async Task SearchByBarcodeAsync_ThrowsDiscogsException_OnMalformedJson()
    {
        var client = CreateClient(HttpStatusCode.OK, "{ not valid json");

        await Assert.ThrowsAsync<DiscogsException>(() => client.SearchByBarcodeAsync("078221870429"));
    }

    [Fact]
    public void Constructing_DoesNotDoubleUserAgent_WhenTwoClientsShareOneHttpClient()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, """{ "results": [] }"""))
        {
            BaseAddress = new Uri(DiscogsClient.BaseUrl),
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("whatinator-tests/1.0 ( test@example.com )");

        _ = new DiscogsClient(httpClient);
        var headerCountAfterFirstConstruction = httpClient.DefaultRequestHeaders.UserAgent.Count;

        _ = new DiscogsClient(httpClient);

        Assert.Equal(headerCountAfterFirstConstruction, httpClient.DefaultRequestHeaders.UserAgent.Count);
    }

    private static DiscogsClient CreateClient(HttpStatusCode statusCode, string responseBody) =>
        new(new HttpClient(new StubHttpMessageHandler(statusCode, responseBody)) { BaseAddress = new Uri(DiscogsClient.BaseUrl) });
}
