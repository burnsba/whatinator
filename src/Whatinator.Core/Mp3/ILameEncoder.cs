namespace Whatinator.Core.Mp3;

/// <summary>
/// The single-track MP3 encode operation <see cref="Mp3Packager"/> depends
/// on. Exists so that class's orchestration/sequencing logic can be
/// unit-tested against a fake implementation instead of the real,
/// process-spawning <see cref="LameEncoder"/> -- same intent as
/// <see cref="Whatinator.Core.Flac.IFlacEncoder"/> for
/// <see cref="Whatinator.Core.Rip.WhatinatorRipRunner"/>.
/// </summary>
public interface ILameEncoder
{
    /// <summary>Encodes one track to V0 MP3, tagged.</summary>
    /// <param name="options">The encode options.</param>
    /// <param name="standardOutput">The stream to relay lame's stdout into.</param>
    /// <param name="standardError">The stream to relay lame's stderr into.</param>
    /// <param name="cancellationToken">A token to cancel the encode.</param>
    /// <returns>The encode's outcome, including lame's stderr filtered down to its final summary for the MP3 log (see <see cref="LameEncodeResult.CapturedOutput"/>).</returns>
    Task<LameEncodeResult> EncodeAsync(
        LameEncodeOptions options,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken = default);
}
