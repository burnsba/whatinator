using System.Diagnostics;
using System.Text;
using Whatinator.Core.Rip;

namespace Whatinator.Core.Mp3;

/// <summary>
/// Drives <c>lame</c> as a subprocess to encode one FLAC track to MP3 V0,
/// fully tagged, in a single invocation -- this system's <c>lame</c>
/// build decodes FLAC input directly, and its ID3v2 tag options cover
/// everything this project needs, confirmed live before implementing
/// (see <c>docs/plan/implementation/phase-007.md</c> § Research
/// findings). Deliberately doesn't embed cover art in the MP3's ID3 tag
/// (user decision, phase 007 UAT) -- cover art is still copied alongside
/// the MP3s at the container level by <see cref="Mp3Packager"/>.
/// </summary>
public sealed class LameEncoder
{
    /// <summary>Runs <c>lame</c> with the given options, relaying its stdout/stderr live.</summary>
    /// <param name="options">The encode options.</param>
    /// <param name="standardOutput">The stream to relay lame's stdout into.</param>
    /// <param name="standardError">The stream to relay lame's stderr into.</param>
    /// <param name="cancellationToken">A token to cancel the encode.</param>
    /// <returns>The encode's outcome, including a raw capture of lame's stderr (confirmed live: lame writes everything -- banner, progress, tag/ReplayGain summary -- to stderr, never stdout) for the MP3 log.</returns>
    public async Task<LameEncodeResult> EncodeAsync(
        LameEncodeOptions options,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        using var process = new Process { StartInfo = BuildStartInfo(options) };
        process.Start();

        using var capturedError = new MemoryStream();
        var relayOutTask = ProcessOutputRelay.RelayAsync(process.StandardOutput.BaseStream, standardOutput, cancellationToken);
        var relayErrTask = ProcessOutputRelay.RelayAsync(process.StandardError.BaseStream, standardError, cancellationToken, capturedError);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(relayOutTask, relayErrTask).ConfigureAwait(false);

        return new LameEncodeResult(process.ExitCode, Encoding.UTF8.GetString(capturedError.ToArray()));
    }

    /// <summary>Builds the <c>lame</c> process start info for <paramref name="options"/>.</summary>
    /// <param name="options">The encode options.</param>
    /// <returns>The configured start info, not yet started.</returns>
    internal static ProcessStartInfo BuildStartInfo(LameEncodeOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "lame",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var args = startInfo.ArgumentList;
        args.Add("-V0");
        args.Add("--tt");
        args.Add(options.Title);
        args.Add("--ta");
        args.Add(options.Artist);
        args.Add("--tl");
        args.Add(options.Album);
        args.Add("--tv");
        args.Add($"TPE2={options.AlbumArtist}");

        if (options.Year is not null)
        {
            args.Add("--ty");
            args.Add(options.Year);
        }

        args.Add("--tn");
        args.Add($"{options.TrackNumber}/{options.TrackCount}");

        if (options.Genre is not null)
        {
            args.Add("--tg");
            args.Add(options.Genre);
        }

        args.Add(options.InputFlacPath);
        args.Add(options.OutputMp3Path);

        return startInfo;
    }
}
