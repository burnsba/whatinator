using Whatinator.Core.Toc;

namespace Whatinator.Cli;

/// <summary>Formats and prints a <see cref="DiscToc"/> to the console. Shared column layout with the EAC-style rip log's own TOC section (phase 016).</summary>
internal static class TocFormatter
{
    /// <summary>CD frames per second -- see <see cref="DiscTocTrack"/>.</summary>
    private const int FramesPerSecond = 75;

    /// <summary>Prints a disc's track table: number, start/length (as time and frames), and pregap/ISRC when known.</summary>
    /// <param name="toc">The table of contents to print.</param>
    public static void Print(DiscToc toc)
    {
        Console.WriteLine($"Tracks: {toc.Tracks.Count}");
        Console.WriteLine();
        Console.WriteLine($"{"Trk",3}  {"Start",-8}  {"Length",-8}  {"Start Sector",12}  {"End Sector",10}");

        foreach (var track in toc.Tracks)
        {
            var length = track.EndFrame - track.StartFrame + 1;
            var suffix = track.IsAudio ? string.Empty : "  (data)";
            Console.WriteLine(
                $"{track.TrackNumber,3}  {FormatMsf(track.StartFrame),-8}  {FormatMsf(length),-8}  {track.StartFrame,12}  {track.EndFrame,10}{suffix}");

            if (track.PregapFrames is int pregap)
            {
                Console.WriteLine($"       pregap: {FormatMsf(pregap)} ({pregap} frames)");
            }

            if (track.Isrc is not null)
            {
                Console.WriteLine($"       ISRC: {track.Isrc}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Leadout: {FormatMsf(toc.LeadoutFrame)} ({toc.LeadoutFrame} frames)");
    }

    /// <summary>Formats a frame count as cdrdao-style <c>MM:SS:FF</c>.</summary>
    /// <param name="frames">The frame count to format.</param>
    /// <returns>The formatted string.</returns>
    private static string FormatMsf(int frames)
    {
        var minutes = frames / (60 * FramesPerSecond);
        var seconds = (frames / FramesPerSecond) % 60;
        var remainingFrames = frames % FramesPerSecond;
        return $"{minutes:00}:{seconds:00}:{remainingFrames:00}";
    }
}
