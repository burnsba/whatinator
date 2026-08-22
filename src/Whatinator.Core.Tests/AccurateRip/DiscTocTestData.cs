using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

/// <summary>
/// A real disc's frame-accurate TOC, taken from the start/end sector table
/// in <c>example/Glorilla - Glorious.log</c> -- used to cross-check
/// <see cref="Whatinator.Core.AccurateRip.AccurateRipDiscId"/>/
/// <see cref="Whatinator.Core.AccurateRip.CddbDiscId"/> against an
/// independent reimplementation of the disc-ID algorithm, rather than only
/// synthetic data.
/// </summary>
internal static class DiscTocTestData
{
    /// <summary>Builds the 15-track, all-audio TOC for Glorilla's "Glorious".</summary>
    /// <returns>The disc's table of contents.</returns>
    public static DiscToc Glorilla()
    {
        int[,] sectors =
        {
            { 1, 0, 6834 },
            { 2, 6835, 16667 },
            { 3, 16668, 29937 },
            { 4, 29938, 42448 },
            { 5, 42449, 53826 },
            { 6, 53827, 70220 },
            { 7, 70221, 87815 },
            { 8, 87816, 105095 },
            { 9, 105096, 119289 },
            { 10, 119290, 128346 },
            { 11, 128347, 141372 },
            { 12, 141373, 153767 },
            { 13, 153768, 165575 },
            { 14, 165576, 178943 },
            { 15, 178944, 191532 },
        };

        var tracks = new List<DiscTocTrack>();
        for (var i = 0; i < sectors.GetLength(0); i++)
        {
            tracks.Add(new DiscTocTrack(sectors[i, 0], sectors[i, 1], sectors[i, 2], IsAudio: true));
        }

        return new DiscToc(tracks);
    }
}
