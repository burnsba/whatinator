using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO.Hashing;
using System.Text;
using Whatinator.Core.AccurateRip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Rip;

/// <summary>
/// Rips a single track by shelling out to <c>cd-paranoia</c> twice (a
/// "test" read and a "copy" read to separate temp files) and comparing
/// their CRC32s -- a defense against a misbehaving drive/cache silently
/// returning different bytes on different reads, independent of
/// and complementary to the online AccurateRip database check
/// (<see cref="AccurateRipClient"/>). On a mismatch, retries the whole
/// two-read cycle up to <see cref="CdParanoiaTrackOptions.MaxRetries"/>
/// times before giving up on the
/// track -- see <see cref="CdParanoiaTrackResult.Degraded"/>. When
/// <see cref="CdParanoiaTrackOptions.Verify"/> is <see langword="false"/>,
/// only the single-pass "fast" read runs -- no copy read, no CRC compare
/// -- and a size check is the sole local verification (see
/// <c>docs/backlog-completed/050-eac-gap-extraction-mode-and-retry-control.md</c>).
/// Either way, <see cref="CdParanoiaTrackOptions.MaxSectorReads"/> and
/// <see cref="CdParanoiaTrackOptions.StallTimeoutSeconds"/> bound how long
/// any one cd-paranoia invocation can spend stuck on a single sector or a
/// hung process respectively.
/// </summary>
public sealed class CdParanoiaTrackReader : ICdParanoiaTrackReader
{
    /// <summary>
    /// A read offset above this many samples can make cd-paranoia
    /// misreport the ripped file's size (a cd-paranoia upstream bug, not
    /// fixable from here -- see <c>libcdio-paranoia</c> issue #14, found via
    /// prior research into this exact behavior). Triggers a warning, not a
    /// rejection.
    /// </summary>
    public const int MaxSafeOffsetSamples = 587;

    /// <summary>44-byte WAV header + <see cref="CdParanoiaProgressLine.WordsPerFrame"/> samples' worth of 16-bit stereo PCM per CD frame.</summary>
    private const int BytesPerFrame = 588 * 4;

    private const int WavHeaderBytes = 44;

    private const int FramesPerSecond = 75;

    /// <summary>Reads <see cref="CdParanoiaTrackOptions.TrackNumber"/>, retrying the test/copy cycle on a CRC32 mismatch or size failure.</summary>
    /// <param name="options">The track to read.</param>
    /// <param name="standardOutput">
    /// The stream to relay cd-paranoia's live progress into (e.g.
    /// <c>Console.OpenStandardError()</c> -- cd-paranoia writes its
    /// progress to stderr).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The read's outcome -- see <see cref="CdParanoiaTrackResult.Degraded"/> for the exhausted-retries case.</returns>
    public async Task<CdParanoiaTrackResult> ReadTrackAsync(
        CdParanoiaTrackOptions options,
        Stream standardOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(standardOutput);

        var track = options.Toc.Tracks.Single(t => t.TrackNumber == options.TrackNumber);

        if (options.Offset > MaxSafeOffsetSamples)
        {
            var message = $"Warning: read offset {options.Offset} exceeds {MaxSafeOffsetSamples} samples -- " +
                "cd-paranoia may misreport file sizes (known upstream bug).";
            await WriteWarningAsync(standardOutput, message, cancellationToken).ConfigureAwait(false);
        }

        if (options.TrackNumber == 99)
        {
            const string message = "Warning: ripping a disc's 99th track may fail outright (known cd-paranoia upstream bug).";
            await WriteWarningAsync(standardOutput, message, cancellationToken).ConfigureAwait(false);
        }

        var destinationPath = Path.GetFullPath(options.DestinationWavPath);
        var destinationDir = Path.GetDirectoryName(destinationPath)
            ?? throw new ArgumentException($"'{options.DestinationWavPath}' has no parent directory.", nameof(options));
        Directory.CreateDirectory(destinationDir);

        (uint Crc32, int? Peak, double? Quality, TimeSpan TestReadElapsed)? matched = null;

        // One reporter spans every cd-paranoia invocation for this track
        // (both "test" and "copy" reads, across every retry) so its
        // per-read state (frame progress, event counts) carries across
        // BeginRead calls instead of restarting fresh -- see
        // CdParanoiaProgressReporter.
        var renderer = new CdParanoiaProgressReporter(standardOutput);

        var (success, attempts) = await RetryAsync(
            options.MaxRetries,
            async ct =>
            {
                matched = await TryOnceAsync(options, track, destinationDir, destinationPath, renderer, ct).ConfigureAwait(false);
                return matched is not null;
            },
            cancellationToken).ConfigureAwait(false);

        return success && matched is not null
            ? new CdParanoiaTrackResult(true, destinationPath, matched.Value.Crc32, matched.Value.Peak, matched.Value.Quality, attempts, matched.Value.TestReadElapsed)
            : new CdParanoiaTrackResult(false, null, null, null, null, attempts);
    }

    /// <summary>
    /// Calls <paramref name="attempt"/> up to <paramref name="maxRetries"/>
    /// times, stopping at the first success. A standalone, dependency-free
    /// seam so the bounded-retry/degraded contract can be unit-tested with
    /// a fake attempt delegate -- no real <c>cd-paranoia</c> process
    /// involved.
    /// </summary>
    /// <param name="maxRetries">The maximum number of attempts.</param>
    /// <param name="attempt">Performs one attempt, returning whether it succeeded.</param>
    /// <param name="cancellationToken">A token to cancel between attempts.</param>
    /// <returns>Whether an attempt succeeded, and how many attempts it took (or <paramref name="maxRetries"/> if none did).</returns>
    internal static async Task<(bool Success, int Attempts)> RetryAsync(
        int maxRetries,
        Func<CancellationToken, Task<bool>> attempt,
        CancellationToken cancellationToken)
    {
        for (var i = 1; i <= maxRetries; i++)
        {
            if (await attempt(cancellationToken).ConfigureAwait(false))
            {
                return (true, i);
            }
        }

        return (false, maxRetries);
    }

    /// <summary>Parses a peak sample level out of <c>sox ... stats</c>'s stderr output.</summary>
    /// <remarks>
    /// Scans for the "Min level"/"Max level" rows by their leading label,
    /// which doesn't break if <c>sox</c> ever adds or
    /// reorders a preceding stats row.
    /// </remarks>
    /// <param name="soxStatsStandardError">The captured stderr text from a <c>sox ... stats</c> run.</param>
    /// <returns>The larger of the absolute min/max sample levels, or <see langword="null"/> if either row wasn't found/parseable.</returns>
    internal static int? ParsePeakLevel(string soxStatsStandardError)
    {
        int? min = null;
        int? max = null;

        foreach (var line in soxStatsStandardError.Split('\n'))
        {
            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3)
            {
                continue;
            }

            if (tokens[0] == "Min" && tokens[1] == "level" && int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minValue))
            {
                min = minValue;
            }
            else if (tokens[0] == "Max" && tokens[1] == "level" && int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxValue))
            {
                max = maxValue;
            }
        }

        return min is null || max is null ? null : Math.Max(Math.Abs(min.Value), Math.Abs(max.Value));
    }

    /// <summary>
    /// Computes a read's track quality from its captured cd-paranoia
    /// progress text: each frame is expected to be read exactly twice
    /// (once forward, once during verification), so quality is
    /// <c>min(frameCount * 2.0 / reads, 1.0)</c> -- a faithful port of the
    /// equivalent progress-parsing logic this port's algorithm research was
    /// cross-checked against. Deliberately omits that source's own extra
    /// accounting state: closely reading its progress-parse routine shows
    /// it's computed but never actually consulted by any branch that
    /// affects <c>reads</c> or the final quality value (only fed to a debug
    /// log line and a commented-out adjustment) -- the same kind of
    /// read-but-unused state <c>CddbDiscId</c>'s port already had to watch
    /// for, see root <c>CLAUDE.md</c> § Gotchas.
    /// </summary>
    /// <param name="capturedStandardError">The read's captured raw stderr text.</param>
    /// <param name="start">The read's start offset within the track, in frames (always <c>0</c> for a whole-track read).</param>
    /// <param name="stop">The read's stop offset within the track, in frames, inclusive.</param>
    /// <param name="trackStartFrame">
    /// The track's own absolute start frame. cd-paranoia's <c>##: ... @ &lt;wordOffset&gt;</c> values are
    /// absolute disc offsets, not track-relative (confirmed live against a real drive; see
    /// <see cref="CdParanoiaProgressReporter.BeginRead"/> and root <c>CLAUDE.md</c> § Gotchas), so every
    /// parsed offset must be converted to track-relative via <see cref="CdParanoiaProgressLine.ToTrackRelativeFrame"/>
    /// before comparing against <paramref name="start"/>/<paramref name="stop"/>, exactly as
    /// <see cref="CdParanoiaProgressReporter.Feed"/> does. Defaults to <c>0</c> for track 1, whose absolute
    /// and track-relative offsets coincide.
    /// </param>
    /// <returns>A quality fraction in <c>(0, 1]</c>, or <see langword="null"/> if no parseable progress lines were captured.</returns>
    internal static double? ComputeQuality(string capturedStandardError, int start, int stop, int trackStartFrame = 0)
    {
        var read = start;
        var reads = 0;

        foreach (var line in capturedStandardError.Split('\n'))
        {
            if (!CdParanoiaProgressLine.TryParse(line, out var function, out var wordOffset) || function != "read")
            {
                continue;
            }

            if (wordOffset % CdParanoiaProgressLine.WordsPerFrame != 0)
            {
                continue;
            }

            var frameOffset = CdParanoiaProgressLine.ToTrackRelativeFrame(wordOffset, trackStartFrame);

            int markStart, markEnd;
            if (frameOffset > read)
            {
                markStart = read;
                markEnd = frameOffset;
            }
            else
            {
                markStart = frameOffset;
                markEnd = frameOffset;
            }

            if (markEnd > stop + 1)
            {
                markEnd = stop + 1;
            }

            if (markStart > stop + 1)
            {
                markStart = stop + 1;
            }

            reads += markEnd - markStart;
            read = frameOffset;
        }

        if (reads == 0)
        {
            return null;
        }

        var frameCount = stop - start + 1;
        return Math.Min(frameCount * 2.0 / reads, 1.0);
    }

    /// <summary>Converts a frame count to cd-paranoia's own <c>hh:mm:ss.ff</c> span format.</summary>
    /// <param name="frames">A frame count (75 frames per second).</param>
    /// <returns>The formatted span component, e.g. <c>00:03:42.65</c>.</returns>
    internal static string FramesToHmsf(int frames)
    {
        var f = frames % FramesPerSecond;
        frames -= f;
        var s = (frames / FramesPerSecond) % 60;
        frames -= s * FramesPerSecond;
        var m = (frames / FramesPerSecond / 60) % 60;
        frames -= m * FramesPerSecond * 60;
        var h = frames / FramesPerSecond / 60 / 60;

        return $"{h:D2}:{m:D2}:{s:D2}.{f:D2}";
    }

    /// <summary>Builds the <c>cd-paranoia</c> process start info for one test or copy read.</summary>
    /// <param name="options">The track read options.</param>
    /// <param name="outputPath">The (not-yet-existing) WAV path cd-paranoia should write to.</param>
    /// <returns>The configured start info, not yet started.</returns>
    internal static ProcessStartInfo BuildStartInfo(CdParanoiaTrackOptions options, string outputPath)
    {
        var track = options.Toc.Tracks.Single(t => t.TrackNumber == options.TrackNumber);
        var stopOffset = track.EndFrame - track.StartFrame;

        var startInfo = new ProcessStartInfo
        {
            FileName = "cd-paranoia",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("--stderr-progress");
        startInfo.ArgumentList.Add($"--sample-offset={options.Offset}");

        if (options.Overread)
        {
            startInfo.ArgumentList.Add("--force-overread");
        }

        // --never-skip[=n] is cd-paranoia's own per-sector retry cap (n=0
        // requests the bare flag, which means "retry forever" -- see man
        // cd-paranoia). Passed unconditionally, in both Verify modes: a
        // sane per-sector bound is a safety net, not something whatinator
        // ever wants left to cd-paranoia's own unflagged default of ~20 (see
        // root CLAUDE.md's --force-overread hang gotcha for why that default
        // wasn't enough to stop a rip running all night on one sector).
        startInfo.ArgumentList.Add(options.MaxSectorReads == 0 ? "--never-skip" : $"--never-skip={options.MaxSectorReads}");

        startInfo.ArgumentList.Add("--force-cdrom-device");
        startInfo.ArgumentList.Add(options.Device);
        startInfo.ArgumentList.Add(
            $"{options.TrackNumber}[{FramesToHmsf(0)}]-{options.TrackNumber}[{FramesToHmsf(stopOffset)}]");
        startInfo.ArgumentList.Add(outputPath);

        return startInfo;
    }

    /// <summary>Builds the <c>sox &lt;file&gt; -n stats -b 16</c> process start info used for peak-level detection.</summary>
    /// <param name="wavPath">The WAV file to analyze.</param>
    /// <returns>The configured start info, not yet started.</returns>
    internal static ProcessStartInfo BuildSoxPeakStartInfo(string wavPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "sox",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add(wavPath);
        startInfo.ArgumentList.Add("-n");
        startInfo.ArgumentList.Add("stats");
        startInfo.ArgumentList.Add("-b");
        startInfo.ArgumentList.Add("16");

        return startInfo;
    }

    /// <summary>
    /// Whether <paramref name="wavPath"/>'s size matches the expected
    /// 44-byte header plus exact PCM byte count for <paramref name="track"/>'s
    /// full frame range -- <c>internal</c> (not just used by this class'
    /// own test/copy cycle) since <see cref="Drive.OffsetFinder"/> reuses it
    /// for its own single-read primitive, see <see cref="RunCdParanoiaAsync"/>.
    /// </summary>
    /// <param name="track">The track being read.</param>
    /// <param name="wavPath">The WAV file to check.</param>
    /// <returns><see langword="true"/> if the file's size matches the expected size for <paramref name="track"/>'s full frame range.</returns>
    internal static bool IsExpectedSize(DiscTocTrack track, string wavPath)
    {
        var frameCount = (long)(track.EndFrame - track.StartFrame + 1);
        var expected = (frameCount * BytesPerFrame) + WavHeaderBytes;
        return new FileInfo(wavPath).Length == expected;
    }

    /// <summary>
    /// Runs one <c>cd-paranoia</c> invocation, relaying its live progress
    /// while also capturing it for post-read quality parsing --
    /// <c>internal</c> (not <c>private</c>) since this is the lower-level
    /// single-read primitive <see cref="Drive.OffsetFinder"/> reuses
    /// directly for its own offset-search reads, which need one plain read
    /// per candidate offset rather than this class' test/copy double-read
    /// cycle (see root <c>CLAUDE.md</c> § Gotchas). Does not manage
    /// <paramref name="renderer"/>'s lifecycle -- the caller must have
    /// already called <see cref="CdParanoiaProgressReporter.BeginRead"/>
    /// for this particular invocation, and decides when (if ever) to call
    /// <see cref="CdParanoiaProgressReporter.Complete"/> -- this lets one
    /// reporter span several invocations (e.g. a test read followed by a
    /// copy read) while carrying its per-track state across them.
    /// </summary>
    /// <param name="options">The track read options.</param>
    /// <param name="outputPath">The WAV path cd-paranoia should write to.</param>
    /// <param name="renderer">
    /// The progress reporter to feed this invocation's <c>##:</c>/other
    /// output to -- also this invocation's only output destination (see
    /// <see cref="TeeRelayLinesAsync"/>) and, when <see cref="CdParanoiaTrackOptions.StallTimeoutSeconds"/>
    /// is nonzero, the source of the stall check itself (see <see cref="StallMonitor"/>).
    /// </param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>
    /// The process's exit code and its captured stderr text. A stalled
    /// invocation (see <see cref="CdParanoiaTrackOptions.StallTimeoutSeconds"/>)
    /// is reported as exit code <c>-1</c> rather than throwing, so callers
    /// treat it exactly like any other failed attempt -- see
    /// <see cref="TryOnceAsync"/>.
    /// </returns>
    internal static async Task<(int ExitCode, string CapturedStandardError)> RunCdParanoiaAsync(
        CdParanoiaTrackOptions options,
        string outputPath,
        CdParanoiaProgressReporter renderer,
        CancellationToken cancellationToken)
    {
        var captured = new StringBuilder();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var stallMonitor = options.StallTimeoutSeconds > 0
            ? new StallMonitor(renderer, TimeSpan.FromSeconds(options.StallTimeoutSeconds), linkedCts)
            : null;

        int exitCode;
        try
        {
            exitCode = await SubprocessRunner.RunAsync(
                BuildStartInfo(options, outputPath),
                (reader, ct) => reader.BaseStream.CopyToAsync(Stream.Null, ct),
                (reader, ct) => TeeRelayLinesAsync(reader, captured, renderer, ct),
                linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // linkedCts fired but the caller's own token didn't -- this was
            // StallMonitor giving up on this invocation, not a real Ctrl-C.
            // The process has already been killed by SubprocessRunner's
            // kill-on-cancel; report a failure rather than rethrowing so the
            // existing MaxRetries cycle (and eventual Degraded/warn-and-
            // continue path) handles it like any other failed read.
            renderer.RelayLine($"Warning: no read progress for {options.StallTimeoutSeconds}s -- aborting this attempt.");
            return (-1, captured.ToString());
        }

        return (exitCode, captured.ToString());
    }

    /// <summary>
    /// Runs one cd-paranoia read cycle for <paramref name="track"/>,
    /// cleaning up its scratch files regardless of outcome -- a test+copy
    /// double read with a CRC32 compare when <see cref="CdParanoiaTrackOptions.Verify"/>
    /// is <see langword="true"/> (the default), or a single read when it's
    /// <see langword="false"/> (single-pass/"fast" mode -- see
    /// <c>docs/backlog-completed/050-eac-gap-extraction-mode-and-retry-control.md</c>).
    /// </summary>
    /// <returns>
    /// The accepted read's CRC32/peak/quality plus that read's own
    /// wall-clock time (<c>TestReadElapsed</c> below), or
    /// <see langword="null"/> on a size failure or (verified mode only) a
    /// CRC32 mismatch.
    /// </returns>
    private static async Task<(uint Crc32, int? Peak, double? Quality, TimeSpan TestReadElapsed)?> TryOnceAsync(
        CdParanoiaTrackOptions options,
        DiscTocTrack track,
        string destinationDir,
        string destinationPath,
        CdParanoiaProgressReporter renderer,
        CancellationToken cancellationToken)
    {
        var testPath = Path.Combine(destinationDir, $"whatinator-{Guid.NewGuid():N}-test.wav");
        var copyPath = options.Verify ? Path.Combine(destinationDir, $"whatinator-{Guid.NewGuid():N}-copy.wav") : null;
        var stopOffset = track.EndFrame - track.StartFrame;

        try
        {
            renderer.BeginRead(stopOffset, startFrame: track.StartFrame, readNumber: 1, totalReads: options.Verify ? 2 : 1);

            // Timed around this first (and, in fast mode, only) read alone,
            // not the copy read or sox below -- see
            // CdParanoiaTrackResult.ElapsedTime for why: this is meant to
            // read as EAC's "Extraction speed" (single-read drive speed),
            // not the wall-clock cost of the whole verify cycle.
            var testReadStopwatch = Stopwatch.StartNew();
            var testRun = await RunCdParanoiaAsync(options, testPath, renderer, cancellationToken).ConfigureAwait(false);
            testReadStopwatch.Stop();
            renderer.Complete();
            if (testRun.ExitCode != 0 || !IsExpectedSize(track, testPath))
            {
                return null;
            }

            uint crc;
            if (options.Verify)
            {
                renderer.BeginRead(stopOffset, startFrame: track.StartFrame, readNumber: 2, totalReads: 2);
                var copyRun = await RunCdParanoiaAsync(options, copyPath!, renderer, cancellationToken).ConfigureAwait(false);
                renderer.Complete();
                if (copyRun.ExitCode != 0 || !IsExpectedSize(track, copyPath!))
                {
                    return null;
                }

                var testCrc = Crc32.HashToUInt32(WavFile.ReadDataChunk(testPath));
                var copyCrc = Crc32.HashToUInt32(WavFile.ReadDataChunk(copyPath!));
                if (testCrc != copyCrc)
                {
                    return null;
                }

                crc = testCrc;
            }
            else
            {
                // No independent second read to compare against -- the size
                // check above is this mode's only local verification.
                // AccurateRip's whole-disc lookup (WhatinatorRipRunner,
                // after every track is read) remains unaffected either way.
                crc = Crc32.HashToUInt32(WavFile.ReadDataChunk(testPath));
            }

            var peak = await TryGetPeakLevelAsync(testPath, cancellationToken).ConfigureAwait(false);
            var quality = ComputeQuality(testRun.CapturedStandardError, 0, stopOffset, track.StartFrame);

            File.Move(testPath, destinationPath, overwrite: true);
            return (crc, peak, quality, testReadStopwatch.Elapsed);
        }
        finally
        {
            if (File.Exists(testPath))
            {
                File.Delete(testPath);
            }

            if (copyPath is not null && File.Exists(copyPath))
            {
                File.Delete(copyPath);
            }
        }
    }

    /// <summary>
    /// Relays cd-paranoia's stderr line by line: every line is appended to
    /// <paramref name="capture"/> (for <see cref="ComputeQuality"/>'s
    /// post-hoc parse) regardless. A <c>##:</c> progress line is fed to
    /// <paramref name="renderer"/>'s <see cref="CdParanoiaProgressReporter.Feed"/>
    /// instead of being written to the console directly -- that's what
    /// stops thousands of raw progress lines from flooding it. Every other
    /// line is run through <see cref="CdParanoiaLiveOutputFilter"/> first:
    /// cd-paranoia's own startup banner is suppressed rather than relayed
    /// at all, and anything that survives the filter (its "Ripping from
    /// sector.../to sector..." announcement, or a genuine error) is handed
    /// to <see cref="CdParanoiaProgressReporter.RelayLine"/> rather than
    /// written directly, since the reporter is this invocation's only
    /// output destination now (it owns the stream).
    /// </summary>
    private static async Task TeeRelayLinesAsync(
        StreamReader source,
        StringBuilder capture,
        CdParanoiaProgressReporter renderer,
        CancellationToken cancellationToken)
    {
        string? line;
        while ((line = await source.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            capture.Append(line).Append('\n');

            if (line.StartsWith("##:", StringComparison.Ordinal))
            {
                renderer.Feed(line);
                continue;
            }

            var filtered = CdParanoiaLiveOutputFilter.Process(line);
            if (filtered is null)
            {
                continue;
            }

            renderer.RelayLine(filtered);
        }
    }

    /// <summary>Writes a single timestamped warning line to <paramref name="standardOutput"/> -- this method is only ever called from within an already-timestamped rip's track loop.</summary>
    private static async Task WriteWarningAsync(Stream standardOutput, string message, CancellationToken cancellationToken) =>
        await StreamLineWriter.WriteLineAsync(standardOutput, message, cancellationToken, timestamped: true).ConfigureAwait(false);

    /// <summary>Best-effort peak sample level via <c>sox &lt;file&gt; -n stats -b 16</c>; <see langword="null"/> if <c>sox</c> is missing or its output can't be parsed.</summary>
    private static async Task<int?> TryGetPeakLevelAsync(string wavPath, CancellationToken cancellationToken)
    {
        try
        {
            string? stderrText = null;
            var exitCode = await SubprocessRunner.RunAsync(
                BuildSoxPeakStartInfo(wavPath),
                (reader, ct) => reader.BaseStream.CopyToAsync(Stream.Null, ct),
                async (reader, ct) => stderrText = await reader.ReadToEndAsync(ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

            return exitCode == 0 ? ParsePeakLevel(stderrText!) : null;
        }
        catch (Win32Exception)
        {
            // sox isn't installed -- best-effort, same contract as CoverArtProcessor's magick fallback.
            return null;
        }
    }
}
