using System.Globalization;
using System.Text;
using Whatinator.Core.Flac;

namespace Whatinator.Core.Mp3;

/// <summary>
/// Writes the MP3 folder's log -- deliberately much shorter than the FLAC
/// folder's EAC-style rip log (see <see cref="FlacPackager"/> and
/// <see cref="Whatinator.Core.Rip.WhatinatorEacLog"/>): <c>init.md</c> only asks for OS
/// info, timestamps, and encoder version/quality for MP3, not drive/rip
/// detail.
/// </summary>
public static class Mp3LogFile
{
    /// <summary>Formats <paramref name="info"/> as MP3 log content.</summary>
    /// <param name="info">The captured system/timing info.</param>
    /// <returns>The formatted text, ready to write to a file.</returns>
    public static string Format(Mp3LogInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var text = new StringBuilder();
        WhatinatorLogHeader.Append(text, info.StartTime);
        text.Append('\n');
        text.Append("Log created by: whatinator\n");
        text.Append('\n');
        text.Append("MP3 conversion phase information:\n");
        text.Append("  OS: ").Append(info.Uname).Append('\n');
        text.Append("  OS (pretty name): ").Append(info.OsPrettyName ?? "-").Append('\n');
        text.Append("  Encoder: ").Append(info.LameVersion).Append('\n');
        text.Append("  Quality: VBR -V0 (highest quality)\n");
        text.Append("  Start time: ").Append(FormatTimestamp(info.StartTime)).Append('\n');
        text.Append("  End time: ").Append(FormatTimestamp(info.EndTime)).Append('\n');

        if (info.Tracks is { Count: > 0 })
        {
            foreach (var track in info.Tracks)
            {
                text.Append('\n');
                text.Append("Track ").Append(track.TrackNumber).Append(" of ").Append(track.TotalTracks).Append(": ").Append(track.Title).Append('\n');
                text.Append(track.LameOutput);
            }
        }

        return text.ToString();
    }

    /// <summary>Formats <paramref name="info"/> and writes it to <paramref name="path"/>.</summary>
    /// <param name="info">The captured system/timing info.</param>
    /// <param name="path">The destination file path.</param>
    public static void Write(Mp3LogInfo info, string path) => File.WriteAllText(path, Format(info));

    /// <summary>Formats a timestamp as ISO 8601 with an explicit UTC offset.</summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>The formatted timestamp.</returns>
    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
}
