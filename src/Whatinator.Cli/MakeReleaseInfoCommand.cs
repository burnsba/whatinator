using Whatinator.Core;
using Whatinator.Core.Discogs;
using Whatinator.Core.Metadata;
using Whatinator.Core.MusicBrainz;
using Whatinator.LibDiscId;

namespace Whatinator.Cli;

/// <summary>Implements the <c>make-releaseinfo</c> command.</summary>
internal static class MakeReleaseInfoCommand
{
    /// <summary>
    /// The <c>User-Agent</c> sent with every MusicBrainz/Discogs request.
    /// See <see cref="MusicBrainzClient"/>'s constructor doc for why this
    /// needs to be descriptive.
    /// </summary>
    private const string UserAgent = "whatinator/0.1 ( bethany.whatinator@burnsba.net )";

    /// <summary>
    /// Resolves a release -- either a fresh MusicBrainz/Discogs lookup, or
    /// (with <c>--releaseinfo</c>) the content of a supplied file -- and
    /// writes it to <c>releaseinfo.json</c> either way.
    /// </summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="httpClientFactory">The shared factory to resolve MusicBrainz/Discogs <see cref="HttpClient"/>s from.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory)
    {
        var dest = CommandLineOptions.GetValue(args, "--dest") ?? ".";
        var releaseInfoPath = CommandLineOptions.GetValue(args, "--releaseinfo");

        ReleaseInfo releaseInfo;
        if (releaseInfoPath is not null)
        {
            releaseInfo = ReleaseInfoFile.Load(releaseInfoPath);
        }
        else
        {
            var resolved = await LookUpFromDiscAsync(args, httpClientFactory).ConfigureAwait(false);
            if (resolved is null)
            {
                return 1;
            }

            releaseInfo = resolved;
        }

        var outputPath = Path.Combine(dest, "releaseinfo.json");
        try
        {
            Directory.CreateDirectory(dest);
            ReleaseInfoFile.Save(releaseInfo, outputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed to write to {dest}: {ex.Message}");
            return 1;
        }

        var trackCount = releaseInfo.Media.Sum(medium => medium.Tracks.Count);
        Console.WriteLine($"{releaseInfo.Artist} - {releaseInfo.Title} ({releaseInfo.Media.Count} disc(s), {trackCount} tracks)");
        Console.WriteLine($"Wrote {outputPath}");
        return 0;
    }

    /// <summary>
    /// Reads the disc, looks it up on MusicBrainz, resolves zero/one/many
    /// matches, and enriches with a Discogs match if possible. Reused by
    /// <see cref="PipelineCommand"/> (phase 008) so the full pipeline can
    /// resolve a release the same way <c>make-releaseinfo</c> does, without
    /// duplicating the disc-read/MusicBrainz/Discogs/picker logic.
    /// </summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="httpClientFactory">The shared factory to resolve MusicBrainz/Discogs <see cref="HttpClient"/>s from.</param>
    /// <returns>The resolved release, or <see langword="null"/> if the caller should exit with an error (already printed).</returns>
    internal static async Task<ReleaseInfo?> LookUpFromDiscAsync(string[] args, IHttpClientFactory httpClientFactory)
    {
        var device = CommandLineOptions.GetValue(args, "--device", "-d") ?? ConfigLoader.Load().Device;

        Disc disc;
        try
        {
            disc = DiscReader.Read(device);
        }
        catch (DiscIdException ex)
        {
            Console.Error.WriteLine($"Failed to read disc from {device}: {ex.Message}");
            return null;
        }

        var musicBrainzClient = new MusicBrainzClient(UserAgent, httpClientFactory.CreateClient("musicbrainz"), onRetry: ReportMusicBrainzRetry);
        var service = new MetadataService(musicBrainzClient);

        ReleaseInfo releaseInfo;
        try
        {
            var result = await service.LookupByDiscIdAsync(disc.Id).ConfigureAwait(false);
            switch (result.Status)
            {
                case MetadataLookupStatus.Found:
                    releaseInfo = result.ReleaseInfo!;
                    break;
                case MetadataLookupStatus.Ambiguous:
                    var chosen = ConsolePicker.PromptForSelection(
                        $"Found {result.Candidates!.Count} matching releases:",
                        result.Candidates!,
                        DescribeMusicBrainzCandidate,
                        allowSkip: false);
                    if (chosen is null)
                    {
                        Console.Error.WriteLine("No selection made.");
                        return null;
                    }

                    releaseInfo = await service.ResolveAsync(chosen.MusicBrainzReleaseId).ConfigureAwait(false);
                    break;
                default:
                    Console.WriteLine("No MusicBrainz release matched this disc. Known disc info:");
                    Console.WriteLine();
                    DiscInfoFormatter.Print(disc);
                    return null;
            }
        }
        catch (MusicBrainzException ex)
        {
            Console.Error.WriteLine($"MusicBrainz lookup failed: {ex.Message}");
            return null;
        }

        return await EnrichWithDiscogsAsync(releaseInfo, httpClientFactory).ConfigureAwait(false);
    }

    /// <summary>
    /// Best-effort Discogs enrichment: searches by barcode (if known) and,
    /// on zero or one match, uses it automatically; on multiple matches,
    /// prompts with a skip option. A Discogs failure never fails the
    /// command -- it's caught and logged, and the release is returned
    /// unenriched, per <c>init.md</c>'s "shouldn't be a blocking issue"
    /// requirement for Discogs.
    /// </summary>
    /// <param name="releaseInfo">The MusicBrainz-resolved release to enrich.</param>
    /// <param name="httpClientFactory">The shared factory to resolve the Discogs <see cref="HttpClient"/> from.</param>
    /// <returns><paramref name="releaseInfo"/>, with <see cref="ReleaseInfo.Discogs"/> populated if a match was found and selected.</returns>
    private static async Task<ReleaseInfo> EnrichWithDiscogsAsync(ReleaseInfo releaseInfo, IHttpClientFactory httpClientFactory)
    {
        if (string.IsNullOrWhiteSpace(releaseInfo.Barcode))
        {
            return releaseInfo;
        }

        var discogsClient = new DiscogsClient(UserAgent, httpClientFactory.CreateClient("discogs"));

        IReadOnlyList<DiscogsInfo> candidates;
        try
        {
            candidates = await discogsClient.SearchByBarcodeAsync(releaseInfo.Barcode).ConfigureAwait(false);
        }
        catch (DiscogsException ex)
        {
            Console.Error.WriteLine($"Discogs lookup failed (continuing without it): {ex.Message}");
            return releaseInfo;
        }

        DiscogsInfo? chosen = candidates.Count switch
        {
            0 => null,
            1 => candidates[0],
            _ => ConsolePicker.PromptForSelection(
                $"Found {candidates.Count} matching Discogs releases:",
                candidates,
                DescribeDiscogsCandidate,
                allowSkip: true),
        };

        return chosen is null ? releaseInfo : releaseInfo with { Discogs = chosen };
    }

    /// <summary>Prints a status line for each MusicBrainz retry so a multi-minute backoff doesn't look like a hang.</summary>
    /// <param name="attempt">The attempt number that just failed.</param>
    /// <param name="maxAttempts">The maximum number of attempts before giving up.</param>
    /// <param name="delay">The backoff delay about to be waited before the next attempt.</param>
    /// <param name="ex">The exception that triggered the retry.</param>
    private static void ReportMusicBrainzRetry(int attempt, int maxAttempts, TimeSpan delay, Exception ex) =>
        Console.Error.WriteLine($"MusicBrainz request failed ({ex.Message}) -- retrying in {(int)delay.TotalSeconds}s (attempt {attempt}/{maxAttempts})...");

    /// <summary>Formats one line describing a MusicBrainz candidate for the picker.</summary>
    /// <param name="candidate">The candidate to describe.</param>
    /// <returns>The description.</returns>
    private static string DescribeMusicBrainzCandidate(ReleaseCandidate candidate) =>
        $"{candidate.Artist} - {candidate.Title} ({candidate.Date ?? "?"}, {candidate.Country ?? "?"}) barcode={candidate.Barcode ?? "?"}";

    /// <summary>Formats one line describing a Discogs candidate for the picker.</summary>
    /// <param name="candidate">The candidate to describe.</param>
    /// <returns>The description.</returns>
    private static string DescribeDiscogsCandidate(DiscogsInfo candidate) =>
        $"{candidate.Title} ({candidate.Country ?? "?"}, {candidate.Format ?? "?"}) label={candidate.Label ?? "?"} cat={candidate.CatalogNumber ?? "?"} id={candidate.Id}";
}
