using Whatinator.Core.AccurateRip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class AccurateRipDiscIdTests
{
    [Fact]
    public void Compute_RealDiscToc_MatchesIndependentReferenceValues()
    {
        // Reference values of the AccurateRip disc-ID algorithm against this same TOC.
        var (discId1, discId2) = AccurateRipDiscId.Compute(DiscTocTestData.Glorilla());

        Assert.Equal("0016c2e1", discId1);
        Assert.Equal("0106d71e", discId2);
    }

    [Fact]
    public void Compute_DataTrack_ExcludedFromSumButCountsTowardLeadout()
    {
        var audioOnly = new DiscToc(
        [
            new DiscTocTrack(1, 0, 999, IsAudio: true),
            new DiscTocTrack(2, 1000, 1999, IsAudio: true),
        ]);
        var withTrailingDataTrack = new DiscToc(
        [
            new DiscTocTrack(1, 0, 999, IsAudio: true),
            new DiscTocTrack(2, 1000, 1999, IsAudio: true),
            new DiscTocTrack(3, 2000, 2999, IsAudio: false),
        ]);

        var (audioOnlyId1, audioOnlyId2) = AccurateRipDiscId.Compute(audioOnly);
        var (withDataId1, withDataId2) = AccurateRipDiscId.Compute(withTrailingDataTrack);

        // Leadout moves from 2000 (audio-only) to 3000 (past the data track),
        // so the IDs differ even though the data track itself isn't summed.
        Assert.NotEqual(audioOnlyId1, withDataId1);
        Assert.NotEqual(audioOnlyId2, withDataId2);
    }

    [Fact]
    public void Compute_TrackStartingAtFrameZero_UsesOneAsDiscId2Multiplicand()
    {
        var toc = new DiscToc(
        [
            new DiscTocTrack(1, 0, 999, IsAudio: true),
        ]);

        var (_, discId2) = AccurateRipDiscId.Compute(toc);

        // discId2 = (0 treated as 1) * track 1  +  leadout(1000) * (audioCount(1) + 1)
        var expected = (uint)((1 * 1) + (1000 * 2));
        Assert.Equal(expected.ToString("x8"), discId2);
    }
}
