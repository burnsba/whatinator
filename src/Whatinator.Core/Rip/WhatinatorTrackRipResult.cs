using Whatinator.Core.AccurateRip;

namespace Whatinator.Core.Rip;

/// <summary>One track's outcome within a <see cref="WhatinatorRipRunner.RipAsync"/> call.</summary>
/// <param name="TrackNumber">The track's 1-based position on the disc.</param>
/// <param name="Degraded">
/// Whether this track could not be read after exhausting
/// <see cref="WhatinatorRipOptions.MaxRetries"/> cd-paranoia test/copy
/// cycles -- same "warn, skip, continue" contract as
/// <see cref="CdParanoiaTrackResult.Degraded"/>. Every other field is
/// <see langword="null"/> when this is <see langword="true"/>.
/// </param>
/// <param name="FlacFilePath">The track's encoded FLAC file, or <see langword="null"/> if <paramref name="Degraded"/>.</param>
/// <param name="WavFilePath">The track's retained WAV file (only present when <see cref="WhatinatorRipOptions.KeepWav"/> was set), or <see langword="null"/> otherwise.</param>
/// <param name="Crc32">The accepted read's local test/copy CRC32, from <see cref="CdParanoiaTrackResult.Crc32"/>.</param>
/// <param name="Peak">The accepted read's peak sample level, from <see cref="CdParanoiaTrackResult.Peak"/>.</param>
/// <param name="Quality">The accepted read's track quality, from <see cref="CdParanoiaTrackResult.Quality"/>.</param>
/// <param name="Attempts">How many test/copy cycles were run for this track.</param>
/// <param name="AccurateRip">
/// This track's AccurateRip database match, or <see langword="null"/> when
/// <paramref name="Degraded"/>, or when the whole-disc AccurateRip lookup
/// wasn't attempted (see <see cref="WhatinatorRipResult.AccurateRipFound"/>'s
/// remarks) or found nothing.
/// </param>
/// <param name="ElapsedTime">
/// The accepted attempt's single "test" read time, from
/// <see cref="CdParanoiaTrackResult.ElapsedTime"/> -- see that member for
/// why the copy read/sox/retries are deliberately excluded -- or
/// <see langword="null"/> when <paramref name="Degraded"/>. Feeds the
/// EAC-style rip log's "Extraction speed" field
/// (<see cref="Rip.WhatinatorEacLog"/>).
/// </param>
public sealed record WhatinatorTrackRipResult(
    int TrackNumber,
    bool Degraded,
    string? FlacFilePath,
    string? WavFilePath,
    uint? Crc32,
    int? Peak,
    double? Quality,
    int Attempts,
    AccurateRipTrackMatch? AccurateRip = null,
    TimeSpan? ElapsedTime = null);
