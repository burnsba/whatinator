using System.Globalization;
using System.Text;

namespace Whatinator.Core;

/// <summary>
/// The two-line "whatinator V{version} ... extraction log" / "whatinator
/// extraction logfile from {date}, {time}" header shared verbatim by the
/// FLAC rip log (<see cref="Rip.WhatinatorEacLog"/>) and the MP3 log
/// (<see cref="Mp3.Mp3LogFile"/>) -- factored out so the two can't drift
/// apart on this shared piece.
/// </summary>
public static class WhatinatorLogHeader
{
    /// <summary>Appends the header's two lines (with their EAC-style blank-line spacing) to <paramref name="text"/>.</summary>
    /// <param name="text">The log text being built.</param>
    /// <param name="timestamp">The run's start time -- the header's own dated line.</param>
    public static void Append(StringBuilder text, DateTimeOffset timestamp)
    {
        text.Append($"whatinator V{WhatinatorVersion.Current} EAC-style extraction log\n");
        text.Append('\n');
        text.Append($"whatinator extraction logfile from {FormatEacDate(timestamp)}, {timestamp:HH:mm}\n");
    }

    /// <summary>Formats EAC's own date style, e.g. <c>2. November 2024</c> (no leading zero on the day).</summary>
    private static string FormatEacDate(DateTimeOffset timestamp) =>
        $"{timestamp.Day}. {timestamp.ToString("MMMM", CultureInfo.InvariantCulture)} {timestamp.Year}";
}
