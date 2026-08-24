using System.ComponentModel;
using Whatinator.Core;
using Whatinator.Core.AccurateRip;
using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;
using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

namespace Whatinator.Cli;

/// <summary>
/// Implements the <c>rip</c> command: rip-only, one disc, metadata already
/// resolved. This is a single building block, not the whole workflow -- it
/// requires an already-written <c>releaseinfo.json</c> (no MusicBrainz/
/// Discogs lookup of its own), rips exactly one disc (<c>--disc</c> is
/// required for a multi-disc release; there's no looping/disc-swap
/// prompting here), and stops once the rip is done -- no FLAC packaging, no
/// MP3 encoding. Chain <c>flac</c>/<c>mp3</c> afterward yourself if you want
/// those, or just use <c>pipeline</c>, which composes metadata resolution +
/// this same rip step + FLAC packaging + MP3 encoding + multi-disc looping
/// into one command. Use `rip` directly only when you want to run a stage
/// in isolation (e.g. re-ripping one disc without re-touching metadata).
/// </summary>
internal static class RipCommand
{
    /// <summary>Rips the disc in the drive, using an already-resolved <c>releaseinfo.json</c>.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="httpClientFactory">The shared factory to resolve the AccurateRip database <see cref="HttpClient"/> from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken = default)
    {
        var releaseInfoPath = CommandLineOptions.GetValue(args, "--releaseinfo");
        if (releaseInfoPath is null)
        {
            Console.Error.WriteLine("rip requires --releaseinfo <path>.");
            return 1;
        }

        if (!CliArgumentParsing.TryLoadReleaseInfo(releaseInfoPath, out var releaseInfo, out var loadError))
        {
            Console.Error.WriteLine(loadError);
            return 1;
        }

        if (!CliArgumentParsing.TryParseDiscNumber(CommandLineOptions.GetValue(args, "--disc"), out var discError, out var discNumber))
        {
            Console.Error.WriteLine(discError);
            return 1;
        }

        var context = CommandContext.Resolve(args);
        var config = context.Config;
        var device = context.Device;
        var dest = CommandLineOptions.GetValue(args, "--dest") ?? ".";
        var keepWav = CommandLineOptions.HasFlag(args, "--keep-wav");
        var drive = context.ResolveDrive();
        var offset = config.GetReadOffset(drive?.Vendor, drive?.Model, drive?.Release) ?? 0;
        var environment = RipEnvironmentResolver.Resolve(config, drive);

        // Deliberately not disposed: these wrap the process's real stdout/stderr,
        // which Console.Write* below still needs to use.
        var standardOutput = Console.OpenStandardOutput();
        var standardError = Console.OpenStandardError();

        // The one timestamped line before the TOC read -- everything before
        // it (arg/config resolution above) and the TOC/ISRC output right
        // after it are deliberately not timestamped; see root CLAUDE.md §
        // Gotchas.
        Console.WriteLine($"{RipOutputTimestamp.Prefix()}starting: rip {string.Join(' ', args)}");

        var tocReader = new CdrdaoTocReader();
        DiscToc toc;
        try
        {
            toc = await tocReader.ReadAsync(device, fastToc: true, standardError, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or Win32Exception)
        {
            Console.Error.WriteLine($"Failed to read TOC from {device}: {ex.Message}");
            return 1;
        }

        var accurateRipClient = new AccurateRipClient(config.EffectiveUserAgent, httpClientFactory.CreateClient("accuraterip"));
        var runner = new WhatinatorRipRunner(accurateRipClient);
        var options = new WhatinatorRipOptions(
            device, releaseInfo, toc, dest, DiscNumber: discNumber, Offset: offset, KeepWav: keepWav);

        WhatinatorRipResult result;
        var startTime = DateTimeOffset.UtcNow;
        try
        {
            result = await runner.RipAsync(options, standardOutput, standardError, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var endTime = DateTimeOffset.UtcNow;
        if (result.Tracks.Count > 0)
        {
            // Written directly into dest (the rip's own output directory),
            // referencing files at that same location -- unlike `pipeline`
            // (PipelineRunner), this standalone command has no way to know
            // where a later, separately-invoked `flac --dest <path>` will
            // eventually move them to (that's an independent CLI invocation
            // with its own --dest, not necessarily under this one). Picked
            // up by that later `flac` run via FlacPackager's existing
            // "move a .log file if present" mechanism regardless.
            var logOptions = new EacLogOptions(
                releaseInfo,
                result,
                toc,
                dest,
                device,
                environment.DriveVendor,
                environment.DriveModel,
                environment.DriveRelease,
                offset,
                Overread: false,
                environment.CacheDefeat,
                environment.CdParanoiaVersion,
                environment.CdrdaoVersion,
                environment.FlacVersion,
                environment.Uname,
                environment.OsPrettyName,
                startTime,
                endTime);
            var releaseDisplayName = ReleaseFolderNaming.ReleaseDisplayName(releaseInfo);
            WhatinatorEacLog.Write(logOptions, Path.Combine(dest, releaseDisplayName + ".log"));
        }

        Console.WriteLine();
        Console.WriteLine(result.Degraded
            ? $"Rip completed with {result.Tracks.Count(t => t.Degraded)} of {result.Tracks.Count} track(s) skipped after retries."
            : $"Rip succeeded: {result.Tracks.Count} track(s).");

        Console.WriteLine(result.AccurateRipFound
            ? $"AccurateRip: {result.Tracks.Count(t => t.AccurateRip?.IsMatch == true)} of {result.Tracks.Count} track(s) matched the database."
            : "AccurateRip: no database match.");

        return result.Success ? 0 : 1;
    }
}
