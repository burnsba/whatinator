using System.ComponentModel;
using System.Diagnostics;

namespace Whatinator.Core;

/// <summary>
/// Gathers OS/tool version info for the MP3 log. Deliberately independent
/// of the FLAC log's drive/rip info (see root <c>CLAUDE.md</c> § Gotchas) --
/// MP3s can be encoded from a FLAC folder at any time, possibly on a
/// different machine or long after the rip, so this is captured fresh at
/// MP3-encode time rather than derived from anything upstream.
/// </summary>
public static class SystemInfo
{
    /// <summary>Runs <c>uname -a</c> and returns its trimmed output.</summary>
    /// <returns>The uname output, or <c>"unknown"</c> if it couldn't be run.</returns>
    public static string GetUname() => RunCommand("uname", ["-a"]) ?? "unknown";

    /// <summary>Runs <c>lame --version</c> and returns its first line, trimmed.</summary>
    /// <returns>The lame version string, or <c>"unknown"</c> if it couldn't be run.</returns>
    public static string GetLameVersion() => FirstLine(RunCommand("lame", ["--version"]));

    /// <summary>
    /// Runs <c>flac --version</c> and returns its first line, trimmed --
    /// phase 016, for the EAC-style rip log's encoder-version field.
    /// Confirmed live: <c>flac --version</c> exits <c>0</c> and writes to
    /// stdout (<c>flac 1.5.0</c>), unlike <c>cd-paranoia</c>/<c>cdrdao</c> below.
    /// </summary>
    /// <returns>The flac version string, or <c>"unknown"</c> if it couldn't be run.</returns>
    public static string GetFlacVersion() => FirstLine(RunCommand("flac", ["--version"]));

    /// <summary>
    /// Runs <c>cd-paranoia --version</c> and returns its first line, trimmed
    /// -- phase 016. Confirmed live: this dev machine's libcdio-paranoia-based
    /// build exits <c>0</c> but writes its version banner to stderr, not
    /// stdout (same stream cd-paranoia uses for everything else -- see root
    /// <c>CLAUDE.md</c> § Gotchas).
    /// </summary>
    /// <returns>The cd-paranoia version string, or <c>"unknown"</c> if it couldn't be run.</returns>
    public static string GetCdParanoiaVersion() => FirstLine(RunCommand("cd-paranoia", ["--version"], useStandardError: true));

    /// <summary>
    /// Runs bare <c>cdrdao</c> (no arguments) and returns the first line of
    /// its captured stderr, trimmed -- phase 016. Confirmed live: with no
    /// arguments cdrdao prints its version banner as the first line of usage
    /// text to stderr and exits <c>1</c> (not <c>0</c> -- this is cdrdao
    /// refusing to run without a command, not a failure to capture), so
    /// unlike every other <see cref="RunCommand"/> caller here this one
    /// can't require a zero exit code.
    /// </summary>
    /// <returns>The cdrdao version banner line, or <c>"unknown"</c> if it couldn't be run.</returns>
    public static string GetCdrdaoVersion() => FirstLine(RunCommand("cdrdao", arguments: null, useStandardError: true, requireSuccess: false));

    /// <summary>Reads <c>PRETTY_NAME</c> from an os-release file.</summary>
    /// <param name="osReleasePath">The file to read. Defaults to <c>/etc/os-release</c>; overridable for testing.</param>
    /// <returns>The unquoted <c>PRETTY_NAME</c> value, or <see langword="null"/> if the file/field wasn't found.</returns>
    public static string? GetOsPrettyName(string osReleasePath = "/etc/os-release")
    {
        if (!File.Exists(osReleasePath))
        {
            return null;
        }

        const string prefix = "PRETTY_NAME=";
        foreach (var line in File.ReadLines(osReleasePath))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..].Trim('"');
            }
        }

        return null;
    }

    /// <summary>Runs a command with no shell involved and returns the requested stream's trimmed output.</summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">The single argument to pass, or <see langword="null"/> to run with no arguments (e.g. bare <c>cdrdao</c>'s version banner).</param>
    /// <param name="useStandardError">
    /// When <see langword="true"/>, captures/returns stderr instead of
    /// stdout -- several of this project's tools (<c>cd-paranoia</c>,
    /// <c>cdrdao</c>) write their version banner there instead, confirmed
    /// live (see root <c>CLAUDE.md</c> § Gotchas).
    /// </param>
    /// <param name="requireSuccess">
    /// When <see langword="false"/>, returns the captured output regardless
    /// of exit code -- needed for bare <c>cdrdao</c>, which exits <c>1</c>
    /// just for being run with no command, not because it failed to print
    /// its version banner.
    /// </param>
    /// <returns>The trimmed output, or <see langword="null"/> if the command couldn't be run or (when <paramref name="requireSuccess"/>) exited non-zero.</returns>
    /// <remarks>
    /// Reads the wanted and "other" streams concurrently rather than one
    /// after the other -- sequential <c>ReadToEnd</c> calls deadlock if the
    /// unread stream fills its pipe buffer before the wanted one is fully
    /// drained (the same class of bug as <see cref="Rip.SubprocessRunner"/>
    /// exists to avoid; see root <c>CLAUDE.md</c> § Gotchas). Low probability
    /// here since every probe's output is small today, but
    /// <see cref="GetCdrdaoVersion"/> deliberately captures bare <c>cdrdao</c>'s
    /// entire usage text, the largest of the five and the one most likely to
    /// grow. <see langword="internal"/> (not <see langword="private"/>) so
    /// tests can drive a synthetic multi-argument command without a fake
    /// process seam, the same test-seam pattern as
    /// <see cref="CoverArt.CoverArtProcessor.BuildStartInfo"/>.
    /// </remarks>
    internal static string? RunCommand(string fileName, string[]? arguments, bool useStandardError = false, bool requireSuccess = true)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            if (arguments is not null)
            {
                foreach (var argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var wanted = useStandardError ? process.StandardError : process.StandardOutput;
            var other = useStandardError ? process.StandardOutput : process.StandardError;
            var otherTask = other.ReadToEndAsync();
            var output = wanted.ReadToEnd();
            otherTask.GetAwaiter().GetResult();
            process.WaitForExit();
            return requireSuccess && process.ExitCode != 0 ? null : output.Trim();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException)
        {
            return null;
        }
    }

    /// <summary>Returns the first non-empty line of <paramref name="output"/>, trimmed.</summary>
    /// <param name="output">The captured output, or <see langword="null"/>.</param>
    /// <returns>The first line, or <c>"unknown"</c> if <paramref name="output"/> is <see langword="null"/> or has no non-empty line.</returns>
    private static string FirstLine(string? output)
    {
        var firstLine = output?.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstLine?.Trim() ?? "unknown";
    }
}
