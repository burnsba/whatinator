namespace Whatinator.Core.Rip;

/// <summary>
/// The outcome of a <see cref="CdParanoiaTrackReader.ReadTrackAsync"/> call,
/// after its internal test/copy retry cycle has either matched or been
/// exhausted.
/// </summary>
/// <param name="Matched">Whether a test/copy CRC32 pair matched within <see cref="CdParanoiaTrackOptions.MaxRetries"/> attempts.</param>
/// <param name="WavPath">
/// The accepted WAV file's path (the "test" read's file -- kept over the
/// "copy" read's on a match, same file this project always keeps), or
/// <see langword="null"/> when <see cref="Matched"/> is <see langword="false"/>.
/// </param>
/// <param name="Crc32">The accepted read's CRC32 over its PCM data, or <see langword="null"/> on failure.</param>
/// <param name="Peak">
/// The accepted WAV's peak sample level (0-32767 for 16-bit audio), from a
/// best-effort <c>sox ... stats</c> call -- <see langword="null"/> if <c>sox</c>
/// isn't installed or its output couldn't be parsed, on failure, or when
/// <see cref="Matched"/> is <see langword="false"/>.
/// </param>
/// <param name="Quality">
/// The accepted read's track quality (1.0 = every frame read exactly
/// twice, lower as re-reads increase) parsed from cd-paranoia's own
/// progress output -- <see langword="null"/> if it couldn't be computed
/// (no parseable progress lines), or when <see cref="Matched"/> is
/// <see langword="false"/>.
/// </param>
/// <param name="Attempts">How many test/copy cycles were run, whether or not the last one matched.</param>
/// <param name="ElapsedTime">
/// The wall-clock time of the accepted attempt's "test" read only --
/// deliberately excluding the "copy" read, the <c>sox</c> peak analysis, and
/// any earlier failed retries -- or <see langword="null"/> when
/// <see cref="Matched"/> is <see langword="false"/>. Feeds the EAC-style rip
/// log's "Extraction speed" field (<see cref="Rip.WhatinatorEacLog"/>),
/// which is meant to read as EAC's own field does: single-read drive speed,
/// not the wall-clock cost of the whole verify cycle -- timing the whole
/// cycle instead made a clean 16x read log as roughly "8.0 X" and a
/// three-retry track log as "2.7 X", both misleadingly low.
/// </param>
/// <param name="DegradedReason">
/// Why this track is <see cref="Degraded"/>, when that reason is more
/// specific than "exhausted <see cref="CdParanoiaTrackOptions.MaxRetries"/>
/// attempts" -- currently only set when an overread attempt stalled and
/// <see cref="CdParanoiaTrackOptions.SkipOverreadOnStall"/> wasn't given, so
/// the warning can name that flag instead of leaving the user to guess.
/// <see langword="null"/> otherwise, including when <see cref="Matched"/> is
/// <see langword="true"/>.
/// </param>
public sealed record CdParanoiaTrackResult(bool Matched, string? WavPath, uint? Crc32, int? Peak, double? Quality, int Attempts, TimeSpan? ElapsedTime = null, string? DegradedReason = null)
{
    /// <summary>
    /// Whether this track could not be read after exhausting
    /// <see cref="CdParanoiaTrackOptions.MaxRetries"/> attempts -- the
    /// same "warn, skip, continue" contract as <see cref="WhatinatorRipResult.Degraded"/>.
    /// </summary>
    public bool Degraded => !Matched;
}
