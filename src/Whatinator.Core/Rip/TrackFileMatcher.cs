using Whatinator.Core.Metadata;

namespace Whatinator.Core.Rip;

/// <summary>
/// Pairs ripped audio files with their <see cref="TrackInfo"/> by parsing
/// the leading track number out of each filename, rather than assuming the
/// file count matches the track count and zipping positionally. This is
/// what makes a degraded rip (some tracks skipped after
/// <see cref="CdParanoiaTrackReader"/>'s own bounded
/// retries -- see <see cref="WhatinatorRipResult.Degraded"/>) still produce a
/// correctly attributed <c>.m3u</c>/MP3 set for whatever tracks *were*
/// captured, instead of either misattributing a later track's audio to an
/// earlier track's metadata or refusing to process the disc at all. Parses
/// only the run of leading ASCII digits, not a fixed separator after them --
/// <see cref="Whatinator.Core.Naming.TrackFileNaming.BuildBaseFileName"/>
/// always follows the number with a literal <c>" - "</c>, but this
/// deliberately doesn't assume that.
/// </summary>
public static class TrackFileMatcher
{
    /// <summary>Matches <paramref name="files"/> to <paramref name="tracks"/> by leading track number.</summary>
    /// <param name="files">The candidate audio file paths (any order).</param>
    /// <param name="tracks">The medium's expected tracks.</param>
    /// <returns>
    /// One entry per track that has a matching file, in track-number order.
    /// Tracks with no matching file (e.g. skipped during a degraded rip) are
    /// omitted rather than paired with the wrong file.
    /// </returns>
    public static IReadOnlyList<(TrackInfo Track, string FilePath)> Match(
        IReadOnlyList<string> files,
        IReadOnlyList<TrackInfo> tracks)
    {
        var byNumber = new Dictionary<int, string>();
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var digitCount = 0;
            while (digitCount < name.Length && char.IsAsciiDigit(name[digitCount]))
            {
                digitCount++;
            }

            if (digitCount > 0 && int.TryParse(name.AsSpan(0, digitCount), out var number))
            {
                byNumber[number] = file;
            }
        }

        var matched = new List<(TrackInfo Track, string FilePath)>();
        foreach (var track in tracks.OrderBy(t => t.Number))
        {
            if (byNumber.TryGetValue(track.Number, out var file))
            {
                matched.Add((track, file));
            }
        }

        return matched;
    }
}
