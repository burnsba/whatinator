using System.Diagnostics;
using Whatinator.Core.Rip;

namespace Whatinator.Core.Flac;

/// <summary>
/// Drives <c>flac</c> as a subprocess to encode one accepted WAV track to
/// FLAC, tagged, with <c>--verify</c> (FLAC's own decode-and-compare-while-
/// encoding safety check) in a single invocation -- confirmed live against
/// this dev machine's real <c>flac --help</c> (<c>flac 1.5.0</c>) that
/// <c>-T FIELD=VALUE</c> tags can be set at encode time, same
/// one-CLI-invocation-does-everything shape as <see cref="Mp3.LameEncoder"/>.
/// </summary>
public sealed class FlacEncoder : IFlacEncoder
{
    /// <summary>Runs <c>flac</c> with the given options, relaying its stdout/stderr live.</summary>
    /// <param name="options">The encode options.</param>
    /// <param name="standardOutput">The stream to relay flac's stdout into.</param>
    /// <param name="standardError">The stream to relay flac's stderr into.</param>
    /// <param name="cancellationToken">A token to cancel the encode.</param>
    /// <returns>The encode's outcome.</returns>
    public async Task<FlacEncodeResult> EncodeAsync(
        FlacEncodeOptions options,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        var exitCode = await SubprocessRunner.RunAsync(
            BuildStartInfo(options),
            (reader, ct) => ProcessOutputRelay.RelayAsync(reader.BaseStream, standardOutput, ct),
            (reader, ct) => ProcessOutputRelay.RelayAsync(reader.BaseStream, standardError, ct),
            cancellationToken).ConfigureAwait(false);

        return new FlacEncodeResult(exitCode);
    }

    /// <summary>Builds the <c>flac</c> process start info for <paramref name="options"/>.</summary>
    /// <param name="options">The encode options.</param>
    /// <returns>The configured start info, not yet started.</returns>
    internal static ProcessStartInfo BuildStartInfo(FlacEncodeOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "flac",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var args = startInfo.ArgumentList;
        args.Add("--verify");
        args.Add("-o");
        args.Add(options.OutputFlacPath);
        args.Add("-T");
        args.Add($"ARTIST={options.Artist}");
        args.Add("-T");
        args.Add($"ALBUM={options.Album}");
        args.Add("-T");
        args.Add($"TITLE={options.Title}");
        args.Add("-T");
        args.Add($"ALBUMARTIST={options.AlbumArtist}");

        if (options.Year is not null)
        {
            args.Add("-T");
            args.Add($"DATE={options.Year}");
        }

        args.Add("-T");
        args.Add($"TRACKNUMBER={options.TrackNumber}");
        args.Add("-T");
        args.Add($"TRACKTOTAL={options.TrackCount}");

        if (options.Isrc is not null)
        {
            args.Add("-T");
            args.Add($"ISRC={options.Isrc}");
        }

        if (options.Genre is not null)
        {
            args.Add("-T");
            args.Add($"GENRE={options.Genre}");
        }

        args.Add(options.InputWavPath);

        return startInfo;
    }
}
