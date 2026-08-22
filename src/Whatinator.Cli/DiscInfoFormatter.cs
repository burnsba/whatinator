using Whatinator.Core.Metadata;
using Whatinator.LibDiscId;

namespace Whatinator.Cli;

/// <summary>Formats and prints a <see cref="Disc"/>'s TOC information to the console. Shared by <c>disc-info</c> and <c>make-releaseinfo</c>'s no-match path.</summary>
internal static class DiscInfoFormatter
{
    /// <summary>Prints a disc's MusicBrainz/FreeDB IDs and per-track offset/duration listing.</summary>
    /// <param name="disc">The disc to print.</param>
    public static void Print(Disc disc)
    {
        Console.WriteLine($"MusicBrainz disc ID: {disc.Id}");
        Console.WriteLine($"FreeDB disc ID: {disc.FreedbId}");
        Console.WriteLine($"Tracks: {disc.FirstTrack}-{disc.LastTrack} ({disc.Tracks.Count} audio tracks)");
        Console.WriteLine();

        foreach (var track in disc.Tracks)
        {
            Console.WriteLine(
                $"{track.Number,2}  offset={Format(track.Offset)}  duration={Format(track.Duration)}");
        }
    }

    /// <summary>Prints a resolved release's artist/title and per-disc track listing (artist, title, duration).</summary>
    /// <param name="releaseInfo">The release to print.</param>
    public static void PrintRelease(ReleaseInfo releaseInfo)
    {
        Console.WriteLine($"Release: {releaseInfo.Artist} - {releaseInfo.Title} ({releaseInfo.Date ?? "?"})");
        Console.WriteLine($"MusicBrainz: {releaseInfo.MusicBrainzUrl}");

        var multiDisc = releaseInfo.Media.Count > 1;
        foreach (var medium in releaseInfo.Media)
        {
            Console.WriteLine();
            if (multiDisc)
            {
                var header = medium.Subtitle is null ? $"Disc {medium.Position}" : $"Disc {medium.Position} - {medium.Subtitle}";
                Console.WriteLine(header);
            }

            foreach (var track in medium.Tracks)
            {
                Console.WriteLine($"{track.Number,2}  {track.Artist} - {track.Title}  [{Format(track.Duration)}]");
            }
        }
    }

    /// <summary>Formats a <see cref="TimeSpan"/> as <c>mm:ss</c>.</summary>
    /// <param name="span">The value to format.</param>
    /// <returns>The formatted string.</returns>
    private static string Format(TimeSpan span) => span.ToString(@"mm\:ss");
}
