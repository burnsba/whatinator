namespace Whatinator.Core.Rip;

/// <summary>
/// The single-track read operation <see cref="WhatinatorRipRunner"/> depends
/// on. Exists so <see cref="WhatinatorRipRunner"/>'s orchestration/
/// sequencing logic can be unit-tested against a fake implementation instead
/// of the real, process-spawning <see cref="CdParanoiaTrackReader"/>.
/// </summary>
public interface ICdParanoiaTrackReader
{
    /// <summary>Reads one track, retrying the test/copy cycle on a CRC32 mismatch or size failure.</summary>
    /// <param name="options">The track to read.</param>
    /// <param name="standardOutput">The stream to relay live progress into.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The read's outcome.</returns>
    Task<CdParanoiaTrackResult> ReadTrackAsync(
        CdParanoiaTrackOptions options,
        Stream standardOutput,
        CancellationToken cancellationToken = default);
}
