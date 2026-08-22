using Whatinator.Core.AccurateRip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class CddbDiscIdTests
{
    [Fact]
    public void Compute_RealDiscToc_MatchesIndependentReferenceValue()
    {
        // Reference value of this algorithm's CDDB1 digit-sum logic
        // against this same TOC -- matches cd-discid's own published
        // CDDB1 algorithm.
        var cddbId = CddbDiscId.Compute(DiscTocTestData.Glorilla());

        Assert.Equal("c309f90f", cddbId);
    }

    [Fact]
    public void Compute_DataTrack_CountsTowardTrackCountAndLeadout()
    {
        var audioOnly = new DiscToc(
        [
            new DiscTocTrack(1, 0, 999, IsAudio: true),
        ]);
        var withDataTrack = new DiscToc(
        [
            new DiscTocTrack(1, 0, 999, IsAudio: true),
            new DiscTocTrack(2, 1000, 1999, IsAudio: false),
        ]);

        var audioOnlyId = CddbDiscId.Compute(audioOnly);
        var withDataId = CddbDiscId.Compute(withDataTrack);

        // Unlike AccurateRipDiscId, CDDB counts the data track itself (not
        // just its effect on the leadout), so the low byte (track count)
        // differs by exactly 1.
        var audioOnlyValue = Convert.ToUInt32(audioOnlyId, 16);
        var withDataValue = Convert.ToUInt32(withDataId, 16);
        Assert.Equal((byte)(audioOnlyValue & 0xFF) + 1, (int)(withDataValue & 0xFF));
    }
}
