using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Whatinator.Core.Rip;

/// <summary>
/// Reports cd-paranoia's <c>##:</c> progress-line flood as a simple,
/// append-only stream of status lines -- a track announces the sector span
/// it's about to read, a status line prints at most every 20 seconds while
/// it's underway, and a final event-count summary prints once the read
/// exits. See root <c>CLAUDE.md</c> § Gotchas for why an earlier,
/// ANSI-redrawn/alternate-screen-buffer version of this class was
/// reverted: this is a command line tool, not a curses app -- a user needs
/// to be able to Ctrl+C mid-read and still have a normal, scrollable
/// terminal with a readable trace of what happened, not a sophisticated
/// UI. One instance spans every <c>cd-paranoia</c> invocation for a single
/// track (e.g. <see cref="CdParanoiaTrackReader"/>'s test <em>and</em>
/// copy reads) -- <see cref="BeginRead"/> resets the per-read state (frame
/// progress, event counts) between invocations, and a "Read N of M" line
/// is only printed when there's more than one read for the caller to
/// distinguish.
/// </summary>
internal sealed class CdParanoiaProgressReporter
{
    /// <summary>The eight event categories cd-paranoia's <c>--stderr-progress</c> wire format can report, in the mockup's legend order.</summary>
    internal static readonly IReadOnlyList<string> Functions = ["read", "wrote", "verify", "overlap", "finished", "scratch", "skip", "drift"];

    private const int LabelWidth = 2;
    private const int CountWidth = 7;

    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(20);

    private readonly Stream _output;
    private readonly Dictionary<string, int> _counts = Functions.ToDictionary(f => f, _ => 0);
    private readonly Stopwatch _stopwatch = new();

    private int _totalFrames;
    private int _startFrame;
    private int _furthestFrame;
    private int _readNumber = 1;
    private int _totalReads = 1;
    private DateTime _lastStatus = DateTime.MinValue;

    /// <summary>Initializes a new instance of the <see cref="CdParanoiaProgressReporter"/> class.</summary>
    /// <param name="output">The stream to write status lines into.</param>
    public CdParanoiaProgressReporter(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
    }

    /// <summary>Computes the fraction of the current read's track processed so far, in <c>[0, 100]</c>.</summary>
    internal double Percent => _totalFrames <= 0 ? 0 : Math.Clamp(_furthestFrame * 100.0 / _totalFrames, 0, 100);

    /// <summary>
    /// Resets per-read state (frame progress, event counts, the 20-second
    /// status throttle, and the elapsed-time stopwatch) for a new
    /// <c>cd-paranoia</c> invocation against the same track -- e.g. the
    /// "test" read finishing and the "copy" read starting.
    /// </summary>
    /// <param name="stopFrame">The read's stop offset within the track, in frames, inclusive.</param>
    /// <param name="startFrame">
    /// The track's own start frame, in the same absolute-disc-frame numbering as the
    /// <c>##:</c> wire format's <c>@ &lt;wordOffset&gt;</c> values (confirmed live: cd-paranoia reports that
    /// offset against the whole disc, not relative to the requested track, even though
    /// <see cref="CdParanoiaTrackReader.BuildStartInfo"/> requests a track-relative range -- see root
    /// <c>CLAUDE.md</c> § Gotchas). Subtracted from every fed offset before comparing against
    /// <paramref name="stopFrame"/>'s track-relative total, so <see cref="Percent"/>/the "read F / T" line
    /// track progress through *this* track instead of position on the whole disc. Defaults to <c>0</c> for
    /// track 1 (whose absolute and track-relative offsets coincide) and existing tests that model that case.
    /// </param>
    /// <param name="readNumber">This read's 1-based position among <paramref name="totalReads"/> for the current track.</param>
    /// <param name="totalReads">How many reads this track will take in total. A "Read N of M" line is only shown when this is greater than 1.</param>
    public void BeginRead(int stopFrame, int startFrame = 0, int readNumber = 1, int totalReads = 1)
    {
        _totalFrames = stopFrame + 1;
        _startFrame = startFrame;
        _furthestFrame = 0;
        foreach (var function in Functions)
        {
            _counts[function] = 0;
        }

        _readNumber = readNumber;
        _totalReads = totalReads;
        _lastStatus = DateTime.UtcNow;
        _stopwatch.Restart();
    }

    /// <summary>
    /// Feeds one line of cd-paranoia's captured stderr -- a no-op for
    /// anything that isn't a <c>##:</c> progress line. Prints a
    /// <c>[NN%] read F / T</c> status line at most once every 20 seconds.
    /// </summary>
    /// <param name="line">A single line, without its trailing newline.</param>
    public void Feed(string line)
    {
        if (!CdParanoiaProgressLine.TryParse(line, out var function, out var wordOffset))
        {
            return;
        }

        if (_counts.ContainsKey(function))
        {
            _counts[function]++;
        }

        if ((function == "read" || function == "verify") && wordOffset % CdParanoiaProgressLine.WordsPerFrame == 0)
        {
            var frame = (wordOffset / CdParanoiaProgressLine.WordsPerFrame) - _startFrame;
            if (frame > _totalFrames)
            {
                frame = _totalFrames;
            }

            if (frame > _furthestFrame)
            {
                _furthestFrame = frame;
            }
        }

        var now = DateTime.UtcNow;
        if (now - _lastStatus >= StatusInterval)
        {
            _lastStatus = now;
            WriteLine($"[{(int)Percent,3}%] read {_furthestFrame} / {_totalFrames}");
        }
    }

    /// <summary>
    /// Relays a non-<c>##:</c> line cd-paranoia printed that wasn't
    /// recognized as routine boilerplate (see
    /// <see cref="CdParanoiaLiveOutputFilter"/>). cd-paranoia's own real
    /// "Ripping from sector.../to sector..." banner (deliberately relayed
    /// verbatim rather than reformatted -- it already has the exact sector/
    /// track-time numbers) is echoed with blank-line spacing around it and
    /// followed by a "Read N of M" announcement, shown only when this
    /// track has more than one read; anything else (e.g. a genuine error)
    /// is written as-is.
    /// </summary>
    /// <param name="line">A single line, without its trailing newline.</param>
    public void RelayLine(string line)
    {
        if (line.StartsWith("Ripping from sector", StringComparison.Ordinal))
        {
            WriteLine(string.Empty);
            WriteLine(line);
            return;
        }

        if (line.TrimStart().StartsWith("to sector", StringComparison.Ordinal))
        {
            WriteLine(line);
            WriteLine(string.Empty);
            if (_totalReads > 1)
            {
                WriteLine($"Read {_readNumber} of {_totalReads}");
            }

            return;
        }

        WriteLine(line);
    }

    /// <summary>
    /// Prints this read's final event-count summary (the legend, then two
    /// stat rows), a "read [N of M] finished in H:MM:SS" line, and a
    /// trailing blank line. Call once after the <c>cd-paranoia</c>
    /// invocation begun with <see cref="BeginRead"/> has exited.
    /// </summary>
    public void Complete()
    {
        _stopwatch.Stop();

        WriteLine(string.Join(", ", Functions));
        WriteLine(string.Join(
            ' ',
            FormatField("r", _counts["read"]),
            FormatField("w", _counts["wrote"]),
            FormatField("v", _counts["verify"]),
            FormatField("o", _counts["overlap"]),
            FormatField("f", _counts["finished"])));
        WriteLine(string.Join(
            ' ',
            FormatField("sc", _counts["scratch"]),
            FormatField("sk", _counts["skip"]),
            FormatField("dr", _counts["drift"])));

        var readLabel = _totalReads > 1 ? $"read {_readNumber} of {_totalReads}" : "read";
        WriteLine($"{readLabel} finished in {FormatElapsed(_stopwatch.Elapsed)}");
        WriteLine(string.Empty);
    }

    /// <summary>
    /// Formats one <c>[label@count]</c> field -- label right-aligned to
    /// <see cref="LabelWidth"/> characters (so 1- and 2-character labels
    /// like <c>r</c>/<c>sc</c> produce brackets in the same column across
    /// rows) and count right-aligned in a field at least
    /// <see cref="CountWidth"/> characters wide.
    /// </summary>
    /// <param name="label">The field's short label, e.g. <c>r</c>/<c>sc</c>.</param>
    /// <param name="count">The event count to display.</param>
    /// <returns>The formatted field, e.g. <c>[ r@     12]</c>.</returns>
    internal static string FormatField(string label, int count) =>
        $"[{label.PadLeft(LabelWidth)}@{count.ToString(CultureInfo.InvariantCulture).PadLeft(CountWidth)}]";

    /// <summary>Formats an elapsed duration as <c>h:mm:ss</c> (hours omitted when zero, e.g. <c>7:12</c>).</summary>
    /// <param name="elapsed">The elapsed time to format.</param>
    /// <returns>The formatted duration.</returns>
    internal static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.Hours > 0
            ? $"{elapsed.Hours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
            : $"{elapsed.Minutes}:{elapsed.Seconds:D2}";

    private void WriteLine(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(RipOutputTimestamp.Prefix() + text + "\n");
        _output.Write(bytes, 0, bytes.Length);
        _output.Flush();
    }
}
