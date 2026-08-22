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
/// The wall-clock time taken by the whole (possibly-retried) read, or
/// <see langword="null"/> when <see cref="Matched"/> is <see langword="false"/>.
/// Phase 016 -- feeds the EAC-style rip log's "Extraction speed" field
/// (<see cref="Rip.WhatinatorEacLog"/>); nothing before that phase consumed
/// per-track timing.
/// </param>
public sealed record CdParanoiaTrackResult(bool Matched, string? WavPath, uint? Crc32, int? Peak, double? Quality, int Attempts, TimeSpan? ElapsedTime = null)
{
    /// <summary>
    /// Whether this track could not be read after exhausting
    /// <see cref="CdParanoiaTrackOptions.MaxRetries"/> attempts -- the
    /// same "warn, skip, continue" contract as <see cref="WhatinatorRipResult.Degraded"/>.
    /// </summary>
    public bool Degraded => !Matched;
}
