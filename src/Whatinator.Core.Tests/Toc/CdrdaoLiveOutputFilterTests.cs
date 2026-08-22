using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class CdrdaoLiveOutputFilterTests
{
    [Theory]
    [InlineData("Track   Mode    Flags  Start                Length")]
    [InlineData(" 1      AUDIO   0      00:00:32(    32)     03:42:65( 16715)")]
    [InlineData("PQ sub-channel reading (audio track) is supported, data format is BCD.")]
    [InlineData("Raw P-W sub-channel reading (audio track) is supported.")]
    [InlineData("Cooked R-W sub-channel reading (audio track) is supported.")]
    [InlineData("Found pre-gap: 00:00:32")]
    [InlineData("Found ISRC code.")]
    [InlineData("Reading of toc data finished successfully.")]
    public void Process_PassesUnrelatedLinesThroughUnchanged(string line)
    {
        var filter = new CdrdaoLiveOutputFilter();

        Assert.Equal(line, filter.Process(line));
    }

    [Theory]
    [InlineData("Analyzing track 01 (AUDIO): start 00:00:32, length 03:42:65...")]
    [InlineData("Analyzing track 11 (AUDIO): start 43:31:30, length 04:19:02...")]
    public void Process_SuppressesAnalyzingTrackLines(string line)
    {
        var filter = new CdrdaoLiveOutputFilter();

        Assert.Null(filter.Process(line));
    }

    [Fact]
    public void Process_SuppressesCatalogLine_AndSetsSawCatalogLine()
    {
        var filter = new CdrdaoLiveOutputFilter();

        var result = filter.Process("Found disk catalogue number.");

        Assert.Null(result);
        Assert.True(filter.SawCatalogLine);
    }

    [Fact]
    public void SawCatalogLine_IsFalse_WhenCatalogLineNeverSeen()
    {
        var filter = new CdrdaoLiveOutputFilter();

        filter.Process("Reading toc data...");

        Assert.False(filter.SawCatalogLine);
    }
}
