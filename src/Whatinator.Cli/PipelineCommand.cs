using Whatinator.Core;
using Whatinator.Core.AccurateRip;
using Whatinator.Core.CoverArt;
using Whatinator.Core.Metadata;
using Whatinator.Core.Rip;

namespace Whatinator.Cli;

/// <summary>
/// Implements the <c>pipeline</c> command: the full rip → FLAC-packaging →
/// MP3 pipeline in one invocation, looping over every disc of a multi-disc
/// release and prompting between physical disc swaps. This is the
/// "just rip the whole release" entry point -- it resolves metadata itself
/// (MusicBrainz/Discogs, or <c>--releaseinfo</c> to skip that), then for
/// each disc in range runs the same rip step <c>RipCommand</c> exposes
/// standalone, followed by <c>FlacPackager</c>/<c>Mp3Packager</c>. Reach for
/// the standalone <c>rip</c>/<c>flac</c>/<c>mp3</c> commands instead only
/// when you want to run one stage in isolation (e.g. re-ripping a single
/// disc without re-resolving metadata).
/// </summary>
internal static class PipelineCommand
{
    /// <summary>Resolves a release, then rips/packages every disc in range.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="httpClientFactory">The shared factory to resolve MusicBrainz/Discogs/Cover Art Archive/AccurateRip <see cref="HttpClient"/>s from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken = default)
    {
        var options = ParsedOptions.Parse(
            args,
            OptionSpec.Value("--dest"),
            OptionSpec.Value("--releaseinfo"),
            OptionSpec.Value("--device"),
            OptionSpec.Value("--multi"),
            OptionSpec.Flag("--no-flac"),
            OptionSpec.Flag("--no-mp3"),
            OptionSpec.Flag("--keep-wav"),
            OptionSpec.Flag("--fast-toc"),
            OptionSpec.Flag("--overread"));
        if (options.HasErrors)
        {
            foreach (var error in options.Errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        var dest = options.GetValue("--dest") ?? ".";
        var context = CommandContext.Resolve(options);
        var releaseInfo = await ResolveReleaseInfoAsync(options, dest, context, httpClientFactory, cancellationToken).ConfigureAwait(false);
        if (releaseInfo is null)
        {
            return 1;
        }

        int startDisc, endDisc;
        try
        {
            (startDisc, endDisc) = ResolveDiscRange(options, releaseInfo);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var config = context.Config;
        var device = context.Device;
        var noFlac = options.HasFlag("--no-flac");
        var createMp3 = !options.HasFlag("--no-mp3") && config.MakeMp3;
        var keepWav = options.HasFlag("--keep-wav");
        var fastToc = options.HasFlag("--fast-toc");
        var isMultiDisc = releaseInfo.Media.Count > 1;
        var drive = context.ResolveDrive();
        var readOffset = config.GetReadOffset(drive?.Vendor, drive?.Model, drive?.Release);
        var overread = options.HasFlag("--overread") || config.GetOverread(drive?.Vendor, drive?.Model, drive?.Release);
        var environment = RipEnvironmentResolver.Resolve(config, drive);

        var coverArtClient = new CoverArtClient(httpClientFactory.CreateClient("coverart"));
        var accurateRipClient = new AccurateRipClient(httpClientFactory.CreateClient("accuraterip"));
        var pipelineRunner = new PipelineRunner(coverArtClient, accurateRipClient);

        // Deliberately not disposed: these wrap the process's real stdout/stderr,
        // which Console.Write* below still needs to use (same pattern as `rip`/`mp3`).
        var standardOutput = Console.OpenStandardOutput();
        var standardError = Console.OpenStandardError();

        // Printed once, after MusicBrainz/Discogs selection (ResolveReleaseInfoAsync,
        // above) and before the per-disc loop's own TOC read -- everything on
        // either side of this one line is deliberately not timestamped; see
        // root CLAUDE.md § Gotchas.
        Console.WriteLine($"{RipOutputTimestamp.Prefix()}starting: pipeline {string.Join(' ', args)}");

        for (var discNumber = startDisc; discNumber <= endDisc; discNumber++)
        {
            if (isMultiDisc && discNumber > startDisc)
            {
                Console.WriteLine();
                Console.Write($"Insert disc {discNumber} of {releaseInfo.Media.Count} and press Enter...");
                if (Console.ReadLine() is null)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Stdin closed before disc swap was confirmed; aborting.");
                    return 1;
                }
            }

            Console.WriteLine();
            Console.WriteLine(isMultiDisc ? $"=== Disc {discNumber} of {releaseInfo.Media.Count} ===" : "=== Ripping ===");

            PipelineDiscResult result;
            try
            {
                result = await pipelineRunner.RunDiscAsync(
                    new PipelineDiscOptions(
                        releaseInfo,
                        isMultiDisc ? discNumber : null,
                        device,
                        dest,
                        noFlac,
                        createMp3,
                        readOffset,
                        Overread: overread,
                        KeepWav: keepWav,
                        Environment: environment,
                        FastToc: fastToc),
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DirectoryNotFoundException)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            if (!ReportDiscResult(discNumber, result, noFlac))
            {
                return 1;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Pipeline complete: {releaseInfo.Artist} - {releaseInfo.Title}");
        return 0;
    }

    /// <summary>Loads <c>--releaseinfo</c>, or resolves it from the disc in the drive (same as <c>make-releaseinfo</c>), and always writes a copy to <paramref name="dest"/>.</summary>
    /// <param name="options">The caller's already-parsed options.</param>
    /// <param name="dest">Where to write the resolved <c>releaseinfo.json</c>.</param>
    /// <param name="context">The caller's already-resolved config/device, so the disc-lookup path doesn't reload the config a second time per run.</param>
    /// <param name="httpClientFactory">The shared factory to resolve MusicBrainz/Discogs <see cref="HttpClient"/>s from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The resolved release, or <see langword="null"/> if the caller should exit with an error (already printed).</returns>
    private static async Task<ReleaseInfo?> ResolveReleaseInfoAsync(ParsedOptions options, string dest, CommandContext context, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var releaseInfoPath = options.GetValue("--releaseinfo");

        ReleaseInfo releaseInfo;
        if (releaseInfoPath is not null)
        {
            if (!CliArgumentParsing.TryLoadReleaseInfo(releaseInfoPath, out var loaded, out var loadError))
            {
                Console.Error.WriteLine(loadError);
                return null;
            }

            releaseInfo = loaded;
        }
        else
        {
            var resolved = await MakeReleaseInfoCommand.LookUpFromDiscAsync(context, httpClientFactory, cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                return null;
            }

            releaseInfo = resolved;
        }

        try
        {
            Directory.CreateDirectory(dest);
            ReleaseInfoFile.Save(releaseInfo, Path.Combine(dest, "releaseinfo.json"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed to write to {dest}: {ex.Message}");
            return null;
        }

        return releaseInfo;
    }

    /// <summary>Resolves the inclusive disc range this invocation covers.</summary>
    /// <param name="options">The caller's already-parsed options.</param>
    /// <param name="releaseInfo">The resolved release.</param>
    /// <returns>The 1-based (start, end) disc numbers, inclusive.</returns>
    /// <exception cref="ArgumentException"><c>--multi</c> is malformed or out of range for <paramref name="releaseInfo"/>.</exception>
    private static (int Start, int End) ResolveDiscRange(ParsedOptions options, ReleaseInfo releaseInfo)
    {
        var multiArg = options.GetValue("--multi");
        if (multiArg is null)
        {
            return (1, releaseInfo.Media.Count);
        }

        var parts = multiArg.Split('-', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var end))
        {
            throw new ArgumentException($"--multi must be '<start>-<end>' (e.g. 1-3), got '{multiArg}'.");
        }

        if (start < 1 || end < start || end > releaseInfo.Media.Count)
        {
            throw new ArgumentException(
                $"--multi {multiArg} is out of range for '{releaseInfo.Title}' ({releaseInfo.Media.Count} disc(s)).");
        }

        return (start, end);
    }

    /// <summary>Prints one disc's outcome and decides whether the pipeline should continue.</summary>
    /// <param name="discNumber">The disc just processed.</param>
    /// <param name="result">Its pipeline result.</param>
    /// <param name="noFlac">Whether <c>--no-flac</c> was given (changes how the raw rip directory is reported).</param>
    /// <returns><see langword="false"/> if the pipeline should abort (a hard rip failure -- already printed).</returns>
    private static bool ReportDiscResult(int discNumber, PipelineDiscResult result, bool noFlac)
    {
        if (!result.RipResult.Success && !result.RipResult.Degraded)
        {
            Console.Error.WriteLine($"No audio tracks were ripped from disc {discNumber}; aborting.");
            return false;
        }

        if (result.RipResult.Degraded)
        {
            var skipped = result.RipResult.Tracks.Count(t => t.Degraded);
            Console.WriteLine(
                $"Warning: {skipped} of {result.RipResult.Tracks.Count} track(s) on disc {discNumber} could not be " +
                "read after retries -- continuing with whatever was captured.");
        }
        else if (result.RipResult.AccurateRipFound)
        {
            var matched = result.RipResult.Tracks.Count(t => t.AccurateRip?.IsMatch == true);
            Console.WriteLine($"AccurateRip: {matched} of {result.RipResult.Tracks.Count} track(s) on disc {discNumber} matched the database.");
        }
        else
        {
            Console.WriteLine($"AccurateRip: no database match for disc {discNumber}.");
        }

        if (result.FlacResult is not null)
        {
            Console.WriteLine($"FLAC: {result.FlacResult.MovedFlacFileCount} track(s) -> {result.FlacResult.DiscDirectory}");
        }
        else if (noFlac)
        {
            Console.WriteLine($"Raw rip (FLAC, unorganized) kept at: {result.RawRipDirectory}");
        }

        if (result.Mp3Result is not null)
        {
            Console.WriteLine($"MP3: {result.Mp3Result.EncodedTrackCount} track(s) -> {result.Mp3Result.DiscDirectory}");
        }

        return true;
    }
}
