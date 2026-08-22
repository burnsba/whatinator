namespace Whatinator.Core.Flac;

/// <summary>
/// The single-track FLAC encode operation
/// <see cref="Whatinator.Core.Rip.WhatinatorRipRunner"/> depends on. Exists
/// so that class's orchestration/sequencing logic can be unit-tested against
/// a fake implementation instead of the real, process-spawning
/// <see cref="FlacEncoder"/>.
/// </summary>
public interface IFlacEncoder
{
    /// <summary>Encodes one track, tagged, with <c>--verify</c>.</summary>
    /// <param name="options">The encode options.</param>
    /// <param name="standardOutput">The stream to relay flac's stdout into.</param>
    /// <param name="standardError">The stream to relay flac's stderr into.</param>
    /// <param name="cancellationToken">A token to cancel the encode.</param>
    /// <returns>The encode's outcome.</returns>
    Task<FlacEncodeResult> EncodeAsync(
        FlacEncodeOptions options,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken = default);
}
