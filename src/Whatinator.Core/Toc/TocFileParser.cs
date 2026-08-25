using System.Text.RegularExpressions;

namespace Whatinator.Core.Toc;

/// <summary>
/// Parses <c>cdrdao</c>'s own <c>.toc</c> text format (as written by
/// <c>cdrdao read-toc</c>) into a <see cref="DiscToc"/>. Grammar spiked
/// against this machine's real <c>cdrdao read-toc</c> output (both
/// <c>--fast-toc</c> and full-scan modes) and cross-checked against
/// <c>man cdrdao</c>'s "TOC FILES" section -- not a general-purpose
/// <c>.toc</c> reader/writer, only the statements <see cref="DiscToc"/> and
/// the <c>toc</c> command's output need (<c>CD-TEXT</c> blocks are skipped
/// wholesale, never parsed; there is no consumer for that data yet -- see
/// root <c>CLAUDE.md</c>).
/// </summary>
internal static partial class TocFileParser
{
    /// <summary>CD frames per second (1 frame = 1 CD sector = 1/75th of a second).</summary>
    private const int FramesPerSecond = 75;

    /// <summary>Parses raw <c>.toc</c> file text into a <see cref="DiscToc"/>.</summary>
    /// <param name="tocText">The full text of a <c>cdrdao read-toc</c>-produced <c>.toc</c> file.</param>
    /// <param name="fastToc">
    /// Whether this text came from a <c>--fast-toc</c> read -- used only to
    /// compute <see cref="DiscTocTrack.PregapScanned"/>, since the <c>.toc</c>
    /// text itself carries no marker distinguishing "no pregap found" from
    /// "pregap not scanned" for tracks after the first.
    /// </param>
    /// <returns>The parsed, frame-accurate table of contents.</returns>
    /// <exception cref="FormatException">
    /// The text isn't a recognized/supported subset of the <c>.toc</c> grammar
    /// -- a genuine parse failure is preferred over a silent wrong answer.
    /// </exception>
    internal static DiscToc Parse(string tocText, bool fastToc)
    {
        ArgumentNullException.ThrowIfNull(tocText);

        var builders = new List<TrackBuilder>();
        TrackBuilder? current = null;
        var accumulatedFrames = 0;
        var cdTextDepth = 0;
        string? catalogNumber = null;

        foreach (var rawLine in tocText.Split('\n'))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (cdTextDepth > 0)
            {
                cdTextDepth += BraceDelta(line);
                continue;
            }

            var tokens = TokenPattern().Matches(line).Select(m => Unquote(m.Value)).ToArray();
            var keyword = tokens[0];

            switch (keyword)
            {
                case "CD_DA":
                case "CD_ROM":
                case "CD_ROM_XA":
                    break;
                case "CATALOG":
                    catalogNumber = RequireToken(tokens, 1, keyword, line);
                    break;
                case "CD_TEXT":
                    cdTextDepth += BraceDelta(line);
                    if (cdTextDepth <= 0)
                    {
                        cdTextDepth = 0;
                    }

                    break;
                case "TRACK":
                    if (tokens.Length < 2)
                    {
                        throw new FormatException($"Malformed TOC: 'TRACK' line missing a track mode: '{line}'");
                    }

                    current = new TrackBuilder(builders.Count + 1, tokens[1] == "AUDIO", accumulatedFrames);
                    builders.Add(current);
                    break;
                case "NO":
                case "COPY":
                case "PRE_EMPHASIS":
                case "TWO_CHANNEL_AUDIO":
                case "FOUR_CHANNEL_AUDIO":
                case "INDEX":
                    RequireCurrentTrack(current, keyword, line);
                    break;
                case "ISRC":
                    RequireCurrentTrack(current, keyword, line);
                    current!.Isrc = RequireToken(tokens, 1, keyword, line);
                    break;
                case "SILENCE":
                case "ZERO":
                    RequireCurrentTrack(current, keyword, line);
                    accumulatedFrames += ParseFrames(RequireToken(tokens, 1, keyword, line), keyword, line);
                    break;
                case "PREGAP":
                    RequireCurrentTrack(current, keyword, line);
                    var pregapFrames = ParseFrames(RequireToken(tokens, 1, keyword, line), keyword, line);
                    current!.PregapFrames = pregapFrames;
                    accumulatedFrames += pregapFrames;
                    break;
                case "START":
                    RequireCurrentTrack(current, keyword, line);
                    current!.PregapFrames = ParseFrames(RequireToken(tokens, 1, keyword, line), keyword, line);
                    break;
                case "FILE":
                case "AUDIOFILE":
                    RequireCurrentTrack(current, keyword, line);
                    accumulatedFrames += ParseFileLength(tokens, keyword, line);
                    break;
                case "DATAFILE":
                    RequireCurrentTrack(current, keyword, line);
                    accumulatedFrames += ParseFrames(RequireToken(tokens, 2, keyword, line), keyword, line);
                    break;
                default:
                    throw new FormatException($"Malformed TOC: unrecognized statement '{keyword}': '{line}'");
            }
        }

        if (builders.Count == 0)
        {
            throw new FormatException("Malformed TOC: no tracks found.");
        }

        var tracks = new List<DiscTocTrack>(builders.Count);
        for (var i = 0; i < builders.Count; i++)
        {
            var b = builders[i];
            var startFrame = b.TrackStartFrame + (b.PregapFrames ?? 0);
            var endFrame = i < builders.Count - 1
                ? builders[i + 1].TrackStartFrame + (builders[i + 1].PregapFrames ?? 0) - 1
                : accumulatedFrames - 1;

            // Track 1's pregap comes straight off the raw TOC even under a
            // fast read, so it's always "scanned"; every other track's
            // pregap needs the audio-content scan --fast-toc skips, unless
            // one was actually parsed anyway (defensive: trust what the data
            // shows over the caller's flag).
            var pregapScanned = i == 0 || !fastToc || b.PregapFrames is not null;

            tracks.Add(new DiscTocTrack(b.TrackNumber, startFrame, endFrame, b.IsAudio, b.PregapFrames, b.Isrc, pregapScanned));
        }

        return new DiscToc(tracks, catalogNumber);
    }

    /// <summary>Matches a double-quoted string or a run of non-whitespace -- a quote-aware whitespace tokenizer.</summary>
    [GeneratedRegex("\"[^\"]*\"|\\S+")]
    private static partial Regex TokenPattern();

    /// <summary>
    /// Parses a <c>FILE</c>/<c>AUDIOFILE "&lt;name&gt;" &lt;start&gt; [&lt;length&gt;]</c>
    /// statement, returning just the frame length -- the <c>&lt;start&gt;</c>
    /// field (an offset into the named source file, not an absolute disc
    /// position) is deliberately never used; this parser tracks each
    /// track's absolute frame position itself via a running accumulator
    /// instead, since that's the only convention that also holds for
    /// <c>DATAFILE</c> statements (which have no <c>&lt;start&gt;</c> field
    /// at all).
    /// </summary>
    private static int ParseFileLength(string[] tokens, string keyword, string line)
    {
        if (tokens.Length < 4)
        {
            throw new FormatException(
                $"Malformed TOC: '{keyword}' with an omitted length is not supported: '{line}'");
        }

        return ParseFrames(tokens[3], keyword, line);
    }

    /// <summary>Parses an <c>MM:SS:FF</c> position/length into a frame count.</summary>
    private static int ParseFrames(string token, string keyword, string line)
    {
        var parts = token.Split(':');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var minutes)
            || !int.TryParse(parts[1], out var seconds)
            || !int.TryParse(parts[2], out var frames)
            || seconds is < 0 or >= 60
            || frames is < 0 or >= FramesPerSecond)
        {
            throw new FormatException($"Malformed TOC: '{keyword}' expected an MM:SS:FF value, got '{token}': '{line}'");
        }

        return (((minutes * 60) + seconds) * FramesPerSecond) + frames;
    }

    /// <summary>Strips a trailing <c>// comment</c>, if any, from a raw line (outside of quoted strings).</summary>
    private static string StripComment(string line)
    {
        var quoted = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && line[i] == '/' && line[i + 1] == '/')
            {
                return line[..i];
            }
        }

        return line;
    }

    /// <summary>Counts net brace depth change (<c>{</c> minus <c>}</c>) on a line -- used to skip <c>CD_TEXT</c> blocks.</summary>
    private static int BraceDelta(string line) => line.Count(c => c == '{') - line.Count(c => c == '}');

    /// <summary>Strips surrounding double quotes from a token, if present.</summary>
    private static string Unquote(string token) =>
        token.Length >= 2 && token[0] == '"' && token[^1] == '"' ? token[1..^1] : token;

    private static string RequireToken(string[] tokens, int index, string keyword, string line)
    {
        if (index >= tokens.Length)
        {
            throw new FormatException($"Malformed TOC: '{keyword}' is missing a required value: '{line}'");
        }

        return tokens[index];
    }

    private static void RequireCurrentTrack(TrackBuilder? current, string keyword, string line)
    {
        if (current is null)
        {
            throw new FormatException($"Malformed TOC: '{keyword}' appears before any 'TRACK' statement: '{line}'");
        }
    }

    /// <summary>Mutable in-progress state for one track while scanning the file, finalized into a <see cref="DiscTocTrack"/> once the whole file's been read.</summary>
    private sealed class TrackBuilder(int trackNumber, bool isAudio, int trackStartFrame)
    {
        public int TrackNumber { get; } = trackNumber;

        public bool IsAudio { get; } = isAudio;

        /// <summary>The absolute frame where this track's index 0 (including any pregap) begins.</summary>
        public int TrackStartFrame { get; } = trackStartFrame;

        public int? PregapFrames { get; set; }

        public string? Isrc { get; set; }
    }
}
