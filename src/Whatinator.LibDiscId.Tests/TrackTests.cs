namespace Whatinator.LibDiscId.Tests;

public class TrackTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(75, 1)]
    [InlineData(150, 2)]
    [InlineData(750, 10)]
    public void Offset_ConvertsWholeSecondSectorCountsToTimeSpan(int offsetSectors, int expectedSeconds)
    {
        var track = new Track(Number: 1, OffsetSectors: offsetSectors, LengthSectors: 0);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), track.Offset);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(75, 1)]
    [InlineData(750, 10)]
    public void Duration_ConvertsWholeSecondSectorCountsToTimeSpan(int lengthSectors, int expectedSeconds)
    {
        var track = new Track(Number: 1, OffsetSectors: 0, LengthSectors: lengthSectors);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), track.Duration);
    }

    [Fact]
    public void Duration_MatchesKnownTrackFromRealDisc()
    {
        // Track 1 of Annie Lennox - Diva (22030 sectors), as read via the
        // Whatinator.LibDiscId wrapper against real hardware and confirmed
        // against MusicBrainz: 4:53.73.
        var track = new Track(Number: 1, OffsetSectors: 150, LengthSectors: 22030);

        Assert.Equal(TimeSpan.FromSeconds(22030 / 75.0), track.Duration);
        Assert.Equal(TimeSpan.FromSeconds(150 / 75.0), track.Offset);
    }
}
