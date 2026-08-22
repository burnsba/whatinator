namespace Whatinator.LibDiscId;

/// <summary>One audio track's position and length, as read from a disc's TOC.</summary>
/// <param name="Number">The track number (1-based).</param>
/// <param name="OffsetSectors">The track's start offset, in CDDA sectors (75/sec).</param>
/// <param name="LengthSectors">The track's length, in CDDA sectors (75/sec).</param>
public sealed record Track(int Number, int OffsetSectors, int LengthSectors)
{
    /// <summary>The number of CDDA sectors per second of audio.</summary>
    private const int SectorsPerSecond = 75;

    /// <summary>The track's start offset, converted to a <see cref="TimeSpan"/>.</summary>
    public TimeSpan Offset => TimeSpan.FromSeconds((double)OffsetSectors / SectorsPerSecond);

    /// <summary>The track's length, converted to a <see cref="TimeSpan"/>.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)LengthSectors / SectorsPerSecond);
}
