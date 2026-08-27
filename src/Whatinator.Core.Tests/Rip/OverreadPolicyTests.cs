using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class OverreadPolicyTests
{
    private static readonly DiscToc ThreeTrackToc = new([
        new DiscTocTrack(1, 0, 16714, IsAudio: true),
        new DiscTocTrack(2, 16715, 33851, IsAudio: true),
        new DiscTocTrack(3, 33852, 50000, IsAudio: true),
    ]);

    [Fact]
    public void ResolveBoundaryTrackNumber_ReturnsNull_WhenOffsetIsZero()
    {
        Assert.Null(OverreadPolicy.ResolveBoundaryTrackNumber(ThreeTrackToc, 0));
    }

    [Fact]
    public void ResolveBoundaryTrackNumber_ReturnsLastTrack_WhenOffsetIsPositive()
    {
        Assert.Equal(3, OverreadPolicy.ResolveBoundaryTrackNumber(ThreeTrackToc, 6));
    }

    [Fact]
    public void ResolveBoundaryTrackNumber_ReturnsFirstTrack_WhenOffsetIsNegative()
    {
        Assert.Equal(1, OverreadPolicy.ResolveBoundaryTrackNumber(ThreeTrackToc, -6));
    }

    [Fact]
    public void ResolveBoundaryTrackNumber_ReturnsDataTrackNumber_WhenBoundaryTrackIsData()
    {
        var toc = new DiscToc([
            new DiscTocTrack(1, 0, 16714, IsAudio: true),
            new DiscTocTrack(2, 16715, 33851, IsAudio: false),
        ]);

        // OverreadPolicy itself doesn't filter by IsAudio -- callers comparing
        // this against an audio-only track list get that filtering for free.
        Assert.Equal(2, OverreadPolicy.ResolveBoundaryTrackNumber(toc, 6));
    }
}
