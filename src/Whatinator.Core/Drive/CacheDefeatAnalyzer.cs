using System.Diagnostics;
using System.Text.RegularExpressions;
using Whatinator.Core.Rip;

namespace Whatinator.Core.Drive;

/// <summary>
/// Runs <c>cd-paranoia -A</c> (a full analysis of a drive's caching/timing/
/// read behavior) and classifies whether the drive lets cd-paranoia defeat
/// its audio cache -- the same question EAC's "Defeat audio cache" log field
/// answers. Verified live against a real drive on this dev machine
/// (<c>/dev/sr1</c>, ASUS DRW-24F1ST): the analysis writes everything to
/// stderr (stdout stays empty, same convention as <c>cdrdao</c>/other
/// subprocess wrappers in this project) and exits <c>0</c> with "Drive
/// tests OK with Paranoia." present in the captured text.
/// </summary>
public static partial class CacheDefeatAnalyzer
{
    /// <summary>Runs the analysis against <paramref name="device"/>.</summary>
    /// <param name="device">The block device to analyze, e.g. <c>/dev/sr1</c>.</param>
    /// <param name="cancellationToken">A token to cancel the analysis.</param>
    /// <returns>Whether the drive can defeat its audio cache, or <see cref="CacheDefeatResult.Unknown"/> if that couldn't be determined.</returns>
    public static async Task<CacheDefeatResult> AnalyzeAsync(string device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        using var process = new Process { StartInfo = BuildStartInfo(device) };
        process.Start();

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var drainStdoutTask = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
        await ProcessCancellation.WaitForExitOrKillAsync(process, cancellationToken).ConfigureAwait(false);
        var output = await stderrTask.ConfigureAwait(false);
        await drainStdoutTask.ConfigureAwait(false);

        return Classify(process.ExitCode, output);
    }

    /// <summary>
    /// Classifies a completed run's outcome -- a pure function so this
    /// contract is unit-testable without spawning a real <c>cd-paranoia</c>
    /// process. On a clean exit, the drive can
    /// defeat its cache exactly when <see cref="OkPattern"/> matches (never
    /// <see cref="CacheDefeatResult.Unknown"/> in that case); cd-paranoia
    /// itself exits non-zero when it can't determine the answer, in which
    /// case the result is <see cref="CacheDefeatResult.CannotDefeat"/> only
    /// if <see cref="WarningPattern"/> or <see cref="AbortingPattern"/>
    /// actually matched the captured text, else genuinely
    /// <see cref="CacheDefeatResult.Unknown"/> (e.g. no disc in the drive).
    /// </summary>
    /// <param name="exitCode">The process's exit code.</param>
    /// <param name="capturedStandardError">The process's captured stderr text.</param>
    /// <returns>The classified result.</returns>
    internal static CacheDefeatResult Classify(int exitCode, string capturedStandardError)
    {
        if (exitCode == 0)
        {
            return OkPattern().IsMatch(capturedStandardError) ? CacheDefeatResult.CanDefeat : CacheDefeatResult.CannotDefeat;
        }

        return WarningPattern().IsMatch(capturedStandardError) || AbortingPattern().IsMatch(capturedStandardError)
            ? CacheDefeatResult.CannotDefeat
            : CacheDefeatResult.Unknown;
    }

    /// <summary>Builds the <c>cd-paranoia -A</c> process start info.</summary>
    /// <param name="device">The block device to analyze.</param>
    /// <returns>The configured start info, not yet started.</returns>
    internal static ProcessStartInfo BuildStartInfo(string device)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cd-paranoia",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("-A");
        startInfo.ArgumentList.Add("--force-cdrom-device");
        startInfo.ArgumentList.Add(device);

        return startInfo;
    }

    /// <summary>Matches cd-paranoia's success banner, confirmed live: <c>Drive tests OK with Paranoia.</c></summary>
    [GeneratedRegex(@"Drive tests OK with Paranoia\.")]
    private static partial Regex OkPattern();

    /// <summary>Matches cd-paranoia's inconclusive-test warning banner.</summary>
    [GeneratedRegex(@"WARNING! PARANOIA MAY NOT BE")]
    private static partial Regex WarningPattern();

    /// <summary>Matches cd-paranoia's early-abort message.</summary>
    [GeneratedRegex(@"aborting test\.")]
    private static partial Regex AbortingPattern();
}
