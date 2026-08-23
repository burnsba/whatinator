using Whatinator.Core.AccurateRip;
using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Drive;

/// <summary>
/// Auto-detects a drive's sample read offset by ripping a known-good disc's
/// tracks at a series of candidate offsets and checking each against the
/// AccurateRip database -- read the disc's
/// TOC, fetch its AccurateRip database entries once, then for each
/// candidate offset (most-likely-first, see <see cref="CandidateOffsets"/>)
/// read track 1 and check its checksum against every entry; only once
/// track 1 matches does it read every other track except the last
/// (deliberately skipped -- avoids needing overread support just to find an
/// offset) to confirm the whole disc. Never falls back to a partial-match
/// "best guess" -- a wrong offset silently accepted is worse than an honest
/// failure. A single-read primitive, not <see cref="CdParanoiaTrackReader"/>'s
/// test/copy double-read -- offset-finding is "try a bunch of values fast",
/// not a paranoia-grade capture.
/// </summary>
public sealed class OffsetFinder
{
    /// <summary>
    /// Candidate read offsets, most-likely-first. Sourced directly from
    /// AccurateRip's own public drive-offset database
    /// (<c>https://www.accuraterip.com/driveoffsets.htm</c>, pulled
    /// 2026-08-17). Every distinct offset value the page reports, ranked by
    /// the *sum of submission counts* across every drive model reporting
    /// that value (ties broken by the number of distinct drive models
    /// reporting it, then by the offset value itself, for a stable order)
    /// -- i.e. the offset most commonly confirmed correct across the whole
    /// drive population comes first, not just whichever single drive model
    /// has the most submissions. Methodology note, in case these numbers
    /// end up closely matching that other tool's list: that's expected,
    /// not a copy-paste -- cross-checked during implementation and found 99
    /// values in common with the other tool's 102-value list (the 3 that
    /// don't overlap -- -12, 732, 739 -- and this list's 4 new ones -- 36,
    /// 374, 695, 1334 -- reflect drive models added to/removed from
    /// AccurateRip's database in the years since that snapshot was taken),
    /// confirming this is the same underlying data source and ranking
    /// approach, just re-run against current live data. See
    /// <c>docs/plan/implementation/phase-017.md</c> § Research findings for
    /// the full derivation.
    /// </summary>
    public static readonly IReadOnlyList<int> CandidateOffsets =
    [
        6, 667, 48, 102, 103, 30, 12, 96, 618, 738, 594, 98, -472, 696, 733,
        116, 685, 120, 691, 702, 99, 97, 600, 676, 686, 690, 1292, 697, -24, 572,
        704, 1182, 688, -491, 91, 145, 689, 355, 86, 79, -496, 564, 708, 0, 679,
        -1164, 1160, 684, -436, 694, 1194, 94, 681, 106, 678, 117, 943, 692, 92, 680,
        1268, 682, 1279, 1263, 1473, -54, -582, 122, 674, 740, 687, 1272, 1508, -489, 534,
        675, 976, 974, 108, 1303, 111, 1130, 699, 87, 234, 975, -589, -495, -494, 36,
        138, 374, 668, 695, 935, 961, 1127, 1161, 1262, 1334, 1336, 1364, 1776,
    ];

    private readonly IAccurateRipEntryLookup _entryLookup;
    private readonly ICdrdaoTocReader _tocReader;
    private readonly SingleTrackReadDelegate _readTrackOnce;

    /// <summary>Initializes a new instance of the <see cref="OffsetFinder"/> class.</summary>
    /// <param name="entryLookup">The AccurateRip database client to fetch raw entries from.</param>
    public OffsetFinder(IAccurateRipEntryLookup entryLookup)
        : this(entryLookup, new CdrdaoTocReader(), ReadTrackOnceAsync)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OffsetFinder"/> class
    /// with fake <paramref name="tocReader"/>/<paramref name="readTrackOnce"/>
    /// implementations -- a test seam so this class's matching/decision
    /// logic can be exercised without spawning real <c>cdrdao</c>/
    /// <c>cd-paranoia</c> processes or needing a real disc, same intent as
    /// this project's other internal-constructor test seams (e.g.
    /// <see cref="WhatinatorRipRunner"/>'s <c>trackReader</c>/<c>flacEncoder</c>).
    /// </summary>
    /// <param name="entryLookup">The AccurateRip database client to fetch raw entries from.</param>
    /// <param name="tocReader">The TOC reader to use.</param>
    /// <param name="readTrackOnce">The single-track read primitive to use.</param>
    internal OffsetFinder(IAccurateRipEntryLookup entryLookup, ICdrdaoTocReader tocReader, SingleTrackReadDelegate readTrackOnce)
    {
        ArgumentNullException.ThrowIfNull(entryLookup);
        ArgumentNullException.ThrowIfNull(tocReader);
        ArgumentNullException.ThrowIfNull(readTrackOnce);

        _entryLookup = entryLookup;
        _tocReader = tocReader;
        _readTrackOnce = readTrackOnce;
    }

    /// <summary>Reads one track once at a given offset, returning its raw PCM data or <see langword="null"/> if the read failed.</summary>
    /// <param name="device">The block device to read from.</param>
    /// <param name="toc">The disc's table of contents.</param>
    /// <param name="trackNumber">The 1-based track number to read.</param>
    /// <param name="offset">The sample offset to read at.</param>
    /// <param name="standardOutput">The stream to relay live progress into.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The track's raw PCM data, or <see langword="null"/> if the read failed (non-zero exit or unexpected file size).</returns>
    internal delegate Task<byte[]?> SingleTrackReadDelegate(
        string device, DiscToc toc, int trackNumber, int offset, Stream standardOutput, CancellationToken cancellationToken);

    /// <summary>Attempts to find <paramref name="device"/>'s sample read offset against the disc currently in the drive.</summary>
    /// <param name="device">The block device to read from, e.g. <c>/dev/sr1</c>.</param>
    /// <param name="standardOutput">The stream to relay live progress (TOC read, each candidate offset being tried) into.</param>
    /// <param name="cancellationToken">A token to cancel the search.</param>
    /// <returns>The found offset, or a reason none was found.</returns>
    public async Task<OffsetFindResult> FindAsync(string device, Stream standardOutput, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(standardOutput);

        var toc = await _tocReader.ReadAsync(device, fastToc: true, standardOutput, cancellationToken).ConfigureAwait(false);
        var audioTracks = toc.Tracks.Where(t => t.IsAudio).ToList();
        if (audioTracks.Count < 3)
        {
            return new OffsetFindResult(null, OffsetFindFailureReason.TooFewTracks);
        }

        var allEntries = await _entryLookup.GetEntriesAsync(toc, cancellationToken).ConfigureAwait(false);
        var entries = allEntries.Where(e => e.Checksums.Length == audioTracks.Count).ToList();
        if (entries.Count == 0)
        {
            return new OffsetFindResult(null, OffsetFindFailureReason.NoAccurateRipEntry);
        }

        foreach (var candidate in CandidateOffsets)
        {
            await StreamLineWriter.WriteLineAsync(standardOutput, $"Trying offset {candidate}...", cancellationToken).ConfigureAwait(false);

            var track1Pcm = await _readTrackOnce(device, toc, audioTracks[0].TrackNumber, candidate, standardOutput, cancellationToken)
                .ConfigureAwait(false);
            if (track1Pcm is null ||
                !MatchesAnyEntry(entries, 0, AccurateRipChecksum.Compute(track1Pcm, 1, audioTracks.Count)))
            {
                continue;
            }

            // Track 1 alone doesn't confirm the offset -- check every other
            // track except the last (deliberately skipped: avoids needing
            // overread support just to find an offset.
            var checkedCount = 1;
            var matchedCount = 1;
            for (var i = 1; i < audioTracks.Count - 1; i++)
            {
                checkedCount++;
                var pcm = await _readTrackOnce(device, toc, audioTracks[i].TrackNumber, candidate, standardOutput, cancellationToken)
                    .ConfigureAwait(false);
                if (pcm is not null && MatchesAnyEntry(entries, i, AccurateRipChecksum.Compute(pcm, i + 1, audioTracks.Count)))
                {
                    matchedCount++;
                }
            }

            if (matchedCount == checkedCount)
            {
                await StreamLineWriter.WriteLineAsync(
                    standardOutput,
                    $"Offset {candidate} confirmed: {matchedCount} of {matchedCount} track(s) matched.",
                    cancellationToken).ConfigureAwait(false);
                return new OffsetFindResult(candidate, null);
            }

            await StreamLineWriter.WriteLineAsync(
                standardOutput,
                $"Offset {candidate}: only {matchedCount} of {checkedCount} track(s) matched, trying next candidate.",
                cancellationToken).ConfigureAwait(false);
        }

        return new OffsetFindResult(null, OffsetFindFailureReason.NoOffsetMatched);
    }

    /// <summary>Whether a computed checksum matches any entry's checksum at a given track position.</summary>
    /// <param name="entries">Every database entry whose track count matches the disc's audio track count.</param>
    /// <param name="position">The track's 0-based position among audio tracks.</param>
    /// <param name="computed">The track's locally computed v1/v2 checksums.</param>
    private static bool MatchesAnyEntry(List<AccurateRipDbEntry> entries, int position, (uint V1, uint V2) computed) =>
        entries.Any(e => e.Checksums[position] == computed.V1 || e.Checksums[position] == computed.V2);

    /// <summary>The real, process-spawning single-track read: one <c>cd-paranoia</c> invocation (no test/copy cycle), reusing <see cref="CdParanoiaTrackReader"/>'s lower-level primitive.</summary>
    private static async Task<byte[]?> ReadTrackOnceAsync(
        string device, DiscToc toc, int trackNumber, int offset, Stream standardOutput, CancellationToken cancellationToken)
    {
        var track = toc.Tracks.Single(t => t.TrackNumber == trackNumber);
        var outputPath = Path.Combine(Path.GetTempPath(), $"whatinator-offsetfind-{Guid.NewGuid():N}.wav");
        var options = new CdParanoiaTrackOptions(device, toc, trackNumber, outputPath, offset);
        var renderer = new CdParanoiaProgressReporter(standardOutput);

        try
        {
            renderer.BeginRead(track.EndFrame - track.StartFrame, startFrame: track.StartFrame);
            var run = await CdParanoiaTrackReader.RunCdParanoiaAsync(options, outputPath, renderer, cancellationToken)
                .ConfigureAwait(false);
            renderer.Complete();

            // A wrong offset failing to read at the expected size (including
            // the known cd-paranoia upstream FileSizeError-style bug for
            // large offsets -- see root CLAUDE.md § Gotchas) is treated the
            // same as any other non-match: try the next candidate, don't
            // abort the whole search.
            if (run.ExitCode != 0 || !CdParanoiaTrackReader.IsExpectedSize(track, outputPath))
            {
                return null;
            }

            return WavFile.ReadDataChunk(outputPath);
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }
}
