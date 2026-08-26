using System.Globalization;
using System.Text;
using Whatinator.Core.Metadata;

namespace Whatinator.Core;

/// <summary>
/// Formats and writes <c>id.txt</c> files from a <see cref="ReleaseInfo"/>,
/// matching the project's standard cataloging format (see
/// <c>example/id.txt</c>). Pure formatting -- no network or disc I/O.
/// </summary>
public static class IdTextFile
{
    /// <summary>The maximum line length the track listing tries to stay within (see <see cref="AppendTracks"/>).</summary>
    private const int MaxLineLength = 80;

    /// <summary>The minimum gap, in spaces, between a track's title and its duration.</summary>
    private const int TitleDurationGap = 2;

    /// <summary>The width of the "NN " track-number prefix.</summary>
    private const int NumberPrefixWidth = 3;

    /// <summary>
    /// Typographic dash characters that sometimes show up in MusicBrainz/Discogs
    /// data (label names, disambiguation comments, etc.) or in this file's own
    /// literal separators, normalized to a plain ASCII hyphen-minus so the
    /// whole file only ever uses <c>-</c>.
    /// </summary>
    private static readonly char[] NonStandardDashes =
        ['‐', '‑', '‒', '–', '—', '―', '−'];

    /// <summary>Formats <paramref name="releaseInfo"/> as <c>id.txt</c> content.</summary>
    /// <param name="releaseInfo">The release to format.</param>
    /// <param name="upc">
    /// The disc's UPC/EAN catalogue number, from <see cref="Toc.DiscToc.CatalogNumber"/>,
    /// or <see langword="null"/> if unknown -- a physical fact about the
    /// pressing, kept as its own line distinct from <c>release:</c>'s
    /// MusicBrainz/Discogs label catalog number.
    /// </param>
    /// <param name="discIdMatched">
    /// Whether <see cref="ReleaseInfo.MusicBrainzUrl"/> was resolved from a
    /// MusicBrainz disc-ID match (<see langword="true"/>) or a manual
    /// release-URL override entered at the picker (<see langword="false"/>)
    /// -- annotated onto the MusicBrainz URL line so a later reader can tell
    /// whether the match is a provable fact about this physical disc or a
    /// human judgment call. <see langword="null"/> (the default) means this
    /// wasn't tracked for the call that produced <paramref name="releaseInfo"/>
    /// (e.g. it came from a <c>--releaseinfo</c> file) and no annotation is
    /// printed -- same "unknown, so omit" convention as <paramref name="upc"/>.
    /// </param>
    /// <returns>The formatted text, ready to write to a file.</returns>
    public static string Format(ReleaseInfo releaseInfo, string? upc = null, bool? discIdMatched = null)
    {
        ArgumentNullException.ThrowIfNull(releaseInfo);

        var text = new StringBuilder();
        text.Append("artist: ").Append(releaseInfo.Artist).Append('\n');
        text.Append("title: ").Append(releaseInfo.Title).Append('\n');
        text.Append("medium: cd").Append('\n');
        text.Append("release: ").Append(FormatRelease(releaseInfo)).Append('\n');
        text.Append("upc: ").Append(upc ?? "-").Append('\n');
        text.Append("series: -").Append('\n');
        text.Append("format: ").Append(releaseInfo.Discogs?.Format ?? "-").Append('\n');
        text.Append("country: ").Append(releaseInfo.Country ?? "-").Append('\n');
        text.Append("released: ").Append(FormatReleaseDate(releaseInfo.Date)).Append('\n');
        text.Append("genre: ").Append(releaseInfo.Discogs?.Genre ?? "-").Append('\n');
        text.Append("style: ").Append(releaseInfo.Discogs?.Style ?? "-").Append('\n');
        text.Append('\n');

        if (releaseInfo.Discogs is not null)
        {
            text.Append(releaseInfo.Discogs.Url).Append('\n');
        }

        text.Append(releaseInfo.MusicBrainzUrl);
        text.Append(discIdMatched switch
        {
            true => " (disc-id match)",
            false => " (manual override -- not disc-id matched)",
            null => string.Empty,
        });
        text.Append('\n');
        text.Append('\n');

        AppendTrackListing(text, releaseInfo.Media);

        return NormalizeDashes(text.ToString());
    }

    /// <summary>Formats <paramref name="releaseInfo"/> and writes it to <paramref name="path"/>.</summary>
    /// <param name="releaseInfo">The release to format.</param>
    /// <param name="path">The destination file path.</param>
    /// <param name="upc">The disc's UPC/EAN catalogue number -- see <see cref="Format"/>.</param>
    /// <param name="discIdMatched">Whether the MusicBrainz match was disc-ID-based -- see <see cref="Format"/>.</param>
    public static void Write(ReleaseInfo releaseInfo, string path, string? upc = null, bool? discIdMatched = null) =>
        File.WriteAllText(path, Format(releaseInfo, upc, discIdMatched));

    /// <summary>Formats the <c>release:</c> line as <c>{label} - {catalogNumber}</c>, or <c>-</c> if both are unknown.</summary>
    /// <param name="releaseInfo">The release to read label/catalog number from.</param>
    /// <returns>The formatted line value.</returns>
    private static string FormatRelease(ReleaseInfo releaseInfo)
    {
        if (releaseInfo.Label is null && releaseInfo.CatalogNumber is null)
        {
            return "-";
        }

        return $"{releaseInfo.Label ?? "-"} - {releaseInfo.CatalogNumber ?? "-"}";
    }

    /// <summary>
    /// Formats a MusicBrainz release date (full date, year-month, or just a
    /// year) as <c>MMM d, yyyy</c> (e.g. <c>Oct 11, 2024</c>), falling back
    /// progressively for partial dates rather than failing.
    /// </summary>
    /// <param name="date">The raw MusicBrainz date string, or <see langword="null"/>.</param>
    /// <returns>The formatted date, or <c>-</c> if <paramref name="date"/> is null/blank.</returns>
    private static string FormatReleaseDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return "-";
        }

        if (DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fullDate))
        {
            // "MMM d, yyyy" (abbreviated month) to match example/id.txt's "Oct 11, 2024".
            return fullDate.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
        }

        if (DateOnly.TryParseExact(date + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var yearMonth))
        {
            return yearMonth.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        }

        return date;
    }

    /// <summary>Appends every disc's track listing, with a "Disc N -- Subtitle" header when there's more than one disc.</summary>
    /// <param name="text">The buffer to append to.</param>
    /// <param name="media">Every disc in the release.</param>
    private static void AppendTrackListing(StringBuilder text, IReadOnlyList<MediumInfo> media)
    {
        var multiDisc = media.Count > 1;
        for (var i = 0; i < media.Count; i++)
        {
            var medium = media[i];
            if (multiDisc)
            {
                var header = string.IsNullOrWhiteSpace(medium.Subtitle)
                    ? $"Disc {medium.Position}"
                    : $"Disc {medium.Position} - {medium.Subtitle}";
                text.Append(header).Append('\n');
            }

            AppendTracks(text, medium.Tracks);

            if (multiDisc && i < media.Count - 1)
            {
                text.Append('\n');
            }
        }
    }

    /// <summary>
    /// Appends one disc's tracks as <c>NN Title  m:ss</c> lines, with the
    /// duration column aligned to the longest title in this disc -- capped
    /// so no line exceeds <see cref="MaxLineLength"/> unless an individual
    /// title alone is too long to fit regardless of padding.
    /// </summary>
    /// <param name="text">The buffer to append to.</param>
    /// <param name="tracks">The disc's tracks, in order.</param>
    private static void AppendTracks(StringBuilder text, IReadOnlyList<TrackInfo> tracks)
    {
        if (tracks.Count == 0)
        {
            return;
        }

        var durations = tracks.Select(track => FormatDuration(track.Duration)).ToList();
        var maxDurationWidth = durations.Max(duration => duration.Length);
        var maxTitleWidth = tracks.Max(track => track.Title.Length);
        var allowedTitleWidth = Math.Max(0, MaxLineLength - NumberPrefixWidth - TitleDurationGap - maxDurationWidth);
        var titleColumnWidth = Math.Min(maxTitleWidth, allowedTitleWidth);

        for (var i = 0; i < tracks.Count; i++)
        {
            var number = tracks[i].Number.ToString("D2", CultureInfo.InvariantCulture);
            var paddedTitle = tracks[i].Title.PadRight(titleColumnWidth + TitleDurationGap);
            text.Append(number).Append(' ').Append(paddedTitle).Append(durations[i]).Append('\n');
        }
    }

    /// <summary>Formats a duration as <c>m:ss</c> (no leading zero on minutes).</summary>
    /// <param name="duration">The duration to format.</param>
    /// <returns>The formatted duration.</returns>
    private static string FormatDuration(TimeSpan duration) =>
        $"{(int)duration.TotalMinutes}:{duration.Seconds:D2}";

    /// <summary>Replaces every <see cref="NonStandardDashes"/> character in <paramref name="text"/> with a plain ASCII hyphen-minus.</summary>
    /// <param name="text">The formatted <c>id.txt</c> content.</param>
    /// <returns><paramref name="text"/> with only <c>-</c> dashes.</returns>
    private static string NormalizeDashes(string text)
    {
        foreach (var dash in NonStandardDashes)
        {
            text = text.Replace(dash, '-');
        }

        return text;
    }
}
