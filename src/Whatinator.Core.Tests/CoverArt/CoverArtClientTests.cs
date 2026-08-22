using System.Net;
using System.Net.Http.Headers;
using Whatinator.Core.CoverArt;

namespace Whatinator.Core.Tests;

public class CoverArtClientTests
{
    [Fact]
    public async Task TryDownloadFrontCoverAsync_ReturnsJpegBytes_OnSuccess()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4]),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return response;
        });
        var client = new CoverArtClient("whatinator-tests/1.0 ( test@example.com )", new HttpClient(handler));

        var result = await client.TryDownloadFrontCoverAsync("some-release-id");

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Content);
        Assert.Equal(".jpg", result.FileExtension);
    }

    [Fact]
    public async Task TryDownloadFrontCoverAsync_InfersPngExtension()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1]),
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return response;
        });
        var client = new CoverArtClient("whatinator-tests/1.0 ( test@example.com )", new HttpClient(handler));

        var result = await client.TryDownloadFrontCoverAsync("some-release-id");

        Assert.Equal(".png", result!.FileExtension);
    }

    [Fact]
    public async Task TryDownloadFrontCoverAsync_ReturnsNull_On404()
    {
        var client = new CoverArtClient(
            "whatinator-tests/1.0 ( test@example.com )",
            new HttpClient(new StubHttpMessageHandler(HttpStatusCode.NotFound, string.Empty)));

        var result = await client.TryDownloadFrontCoverAsync("no-art-release-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task TryDownloadFrontCoverAsync_ReturnsNull_OnNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var client = new CoverArtClient("whatinator-tests/1.0 ( test@example.com )", new HttpClient(handler));

        var result = await client.TryDownloadFrontCoverAsync("some-release-id");

        Assert.Null(result);
    }
}
