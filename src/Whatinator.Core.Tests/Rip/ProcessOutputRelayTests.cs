using Whatinator.Core.Rip;

namespace Whatinator.Core.Tests;

public class ProcessOutputRelayTests
{
    [Fact]
    public async Task RelayAsync_CopiesBytesExactly_IncludingCarriageReturnsWithoutNewlines()
    {
        // Simulates lame's/cd-paranoia's actual progress output style: repeated
        // \r redraws, no \n at all until the very end. A line-buffered relay would
        // hide all of this until the final newline; RelayAsync must not do that.
        var payload = "Reading track 1 of 11 (1 of 9) ...   0 %\rReading track 1 of 11 (1 of 9) ...   1 %\rDone\n"u8.ToArray();
        using var source = new MemoryStream(payload);
        using var destination = new MemoryStream();

        await ProcessOutputRelay.RelayAsync(source, destination, CancellationToken.None);

        Assert.Equal(payload, destination.ToArray());
    }

    [Fact]
    public async Task RelayAsync_MirrorsBytesIntoCapture_WhenGiven()
    {
        var payload = "some output\n"u8.ToArray();
        using var source = new MemoryStream(payload);
        using var destination = new MemoryStream();
        using var capture = new MemoryStream();

        await ProcessOutputRelay.RelayAsync(source, destination, CancellationToken.None, capture);

        Assert.Equal(payload, destination.ToArray());
        Assert.Equal(payload, capture.ToArray());
    }

    [Fact]
    public async Task RelayAsync_HandlesEmptySource()
    {
        using var source = new MemoryStream();
        using var destination = new MemoryStream();

        await ProcessOutputRelay.RelayAsync(source, destination, CancellationToken.None);

        Assert.Empty(destination.ToArray());
    }
}
