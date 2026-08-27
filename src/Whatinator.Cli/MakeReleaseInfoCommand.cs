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
    /// Resolves a release -- either a fresh MusicBrainz/Discogs lookup, or
    /// (with <c>--releaseinfo</c>) the content of a supplied file -- and
    /// writes it to <c>releaseinfo.json</c> either way.
    /// </summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="httpClientFactory">The shared factory to resolve MusicBrainz/Discogs <see cref="HttpClient"/>s from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken = default)
    {
        var options = ParsedOptions.Parse(
            args,
            OptionSpec.Value("--dest"),
            OptionSpec.Value("--releaseinfo"),
            OptionSpec.Value("--device"));
        if (options.HasErrors)
        {
            foreach (var error in options.Errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        var dest = options.GetValue("--dest") ?? ".";
        var releaseInfoPath = options.GetValue("--releaseinfo");
        var context = CommandContext.Resolve(options);

        ReleaseInfo releaseInfo;
        if (releaseInfoPath is not null)
        {
            if (!CliArgumentParsing.TryLoadReleaseInfo(releaseInfoPath, out var loaded, out var loadError))
            {
                Console.Error.WriteLine(loadError);
                return 1;
            }

            releaseInfo = loaded;
        }
        else
        {
            var resolved = await LookUpFromDiscAsync(context, httpClientFactory, cancellationToken).ConfigureAwait(false);
            if (resolved is null)
            {
                return 1;
            }

            releaseInfo = resolved.ReleaseInfo;
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
    /// <param name="context">The caller's already-resolved config/device, so this doesn't reload the config a second time per run.</param>
    /// <param name="httpClientFactory">The shared factory to resolve MusicBrainz/Discogs <see cref="HttpClient"/>s from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The resolved release, or <see langword="null"/> if the caller should exit with an error (already printed).</returns>
    internal static async Task<ResolvedRelease?> LookUpFromDiscAsync(CommandContext context, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken = default)
    {
        var device = context.Device;

        Disc disc;
        try
        {
#pragma warning disable CA1416 // DiscReader is [SupportedOSPlatform("linux")]; this CLI is Linux-only in practice too (root CLAUDE.md), but isn't itself annotated -- see src/Whatinator.LibDiscId/CLAUDE.md.
            disc = DiscReader.Read(device);
#pragma warning restore CA1416
        }
        catch (DiscIdException ex)
        {
            Console.Error.WriteLine($"Failed to read disc from {device}: {ex.Message}");
            return null;
        }

        var musicBrainzClient = new MusicBrainzClient(httpClientFactory.CreateClient("musicbrainz"), onRetry: ReportMusicBrainzRetry);
        var service = new MetadataService(musicBrainzClient);

        ReleaseInfo releaseInfo;
        bool discIdMatched;
        try
        {
            var result = await service.LookupByDiscIdAsync(disc.Id, cancellationToken).ConfigureAwait(false);
            switch (result.Status)
            {
                case MetadataLookupStatus.Found:
                    releaseInfo = result.ReleaseInfo!;
                    discIdMatched = true;
                    break;
                case MetadataLookupStatus.Ambiguous:
                    var ambiguousResolution = await ResolveAmbiguousMusicBrainzMatchAsync(disc, service, result.Candidates!, cancellationToken).ConfigureAwait(false);
                    if (ambiguousResolution is null)
                    {
                        Console.Error.WriteLine("No selection made.");
                        return null;
                    }

                    (releaseInfo, discIdMatched) = ambiguousResolution.Value;
                    break;
                default:
                    Console.WriteLine("No MusicBrainz release matched this disc. Known disc info:");
                    Console.WriteLine();
                    DiscInfoFormatter.Print(disc);
                    var manualResolution = await ResolveManualOnlyMusicBrainzMatchAsync(disc, service, cancellationToken).ConfigureAwait(false);
                    if (manualResolution is null)
                    {
                        return null;
                    }

                    (releaseInfo, discIdMatched) = manualResolution.Value;
                    break;
            }
        }
        catch (MusicBrainzException ex)
        {
            Console.Error.WriteLine($"MusicBrainz lookup failed: {ex.Message}");
            return null;
        }

        var enriched = await EnrichWithDiscogsAsync(releaseInfo, httpClientFactory, cancellationToken).ConfigureAwait(false);
        return new ResolvedRelease(enriched, discIdMatched);
    }

    /// <summary>
    /// Drives the ambiguous-match picker: lets the user pick one of
    /// <paramref name="candidates"/>, or type <c>m</c> to paste a MusicBrainz
    /// release URL directly (see <see cref="PromptManualMusicBrainzOverrideAsync"/>).
    /// Either way, the resolved release's track count is checked against the
    /// disc's before accepting it -- see root <c>CLAUDE.md</c> § "Track pairing
    /// is positional"; a mismatch here would otherwise surface much later, as
    /// an opaque <see cref="InvalidOperationException"/> from
    /// <c>WhatinatorRipRunner</c>. <see langword="internal"/> as a test seam --
    /// exercising this without a real disc/drive isn't possible via
    /// <see cref="LookUpFromDiscAsync"/> itself, since that calls
    /// <c>DiscReader.Read</c> directly (see <c>src/Whatinator.LibDiscId/CLAUDE.md</c>
    /// § Testing constraints).
    /// </summary>
    /// <param name="disc">The disc being identified, for its audio track count.</param>
    /// <param name="service">The MusicBrainz metadata service to resolve the final pick through.</param>
    /// <param name="candidates">The disc-ID-matched candidates to offer.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <param name="isOutputRedirected">Forwarded to <see cref="ConsolePicker.PromptForSelectionAsync{T}"/> -- overrides the real console-redirection check, for tests.</param>
    /// <returns>
    /// The resolved release and whether it came from a disc-ID-matched
    /// candidate (<see langword="true"/>) or a manual override
    /// (<see langword="false"/>); <see langword="null"/> if no selection was
    /// made (EOF or redirected output -- see <see cref="ConsolePicker"/>).
    /// </returns>
    internal static async Task<(ReleaseInfo ReleaseInfo, bool DiscIdMatched)?> ResolveAmbiguousMusicBrainzMatchAsync(
        Disc disc, MetadataService service, IReadOnlyList<ReleaseCandidate> candidates, CancellationToken cancellationToken, bool? isOutputRedirected = null)
    {
        var expectedTrackCount = disc.Tracks.Count;
        while (true)
        {
            var wasManualOverride = false;
            var chosen = await ConsolePicker.PromptForSelectionAsync(
                $"Found {candidates.Count} matching releases:",
                candidates,
                DescribeMusicBrainzCandidate,
                allowSkip: false,
                manualOverride: async ct =>
                {
                    var candidate = await PromptManualMusicBrainzOverrideAsync(ct).ConfigureAwait(false);
                    wasManualOverride = candidate is not null;
                    return candidate;
                },
                cancellationToken: cancellationToken,
                isOutputRedirected: isOutputRedirected).ConfigureAwait(false);
            if (chosen is null)
            {
                return null;
            }

            var releaseInfo = await ResolveAndValidateTrackCountAsync(service, chosen.MusicBrainzReleaseId, expectedTrackCount, cancellationToken).ConfigureAwait(false);
            if (releaseInfo is null)
            {
                continue;
            }

            return (releaseInfo, !wasManualOverride);
        }
    }

    /// <summary>
    /// Drives the manual-override-only flow used when disc-ID lookup found no
    /// matches at all -- the disc still has to be identified somehow, so this
    /// keeps prompting for a MusicBrainz release URL until one resolves with a
    /// matching track count or the user gives up (blank input/EOF/Ctrl-C).
    /// <see langword="internal"/> for the same test-seam reason as
    /// <see cref="ResolveAmbiguousMusicBrainzMatchAsync"/>.
    /// </summary>
    /// <param name="disc">The disc being identified, for its audio track count.</param>
    /// <param name="service">The MusicBrainz metadata service to resolve the pasted URL through.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The resolved release (always a manual override, so <c>DiscIdMatched</c> is <see langword="false"/>); <see langword="null"/> if the user gave up.</returns>
    internal static async Task<(ReleaseInfo ReleaseInfo, bool DiscIdMatched)?> ResolveManualOnlyMusicBrainzMatchAsync(
        Disc disc, MetadataService service, CancellationToken cancellationToken)
    {
        var expectedTrackCount = disc.Tracks.Count;
        while (true)
        {
            var candidate = await PromptManualMusicBrainzOverrideAsync(cancellationToken).ConfigureAwait(false);
            if (candidate is null)
            {
                return null;
            }

            var releaseInfo = await ResolveAndValidateTrackCountAsync(service, candidate.MusicBrainzReleaseId, expectedTrackCount, cancellationToken).ConfigureAwait(false);
            if (releaseInfo is null)
            {
                continue;
            }

            return (releaseInfo, false);
        }
    }

    /// <summary>Parses the release MBID out of a MusicBrainz release URL. <see langword="internal"/> as a test seam (pure parsing logic, no console I/O).</summary>
    /// <param name="url">The pasted URL, e.g. <c>https://musicbrainz.org/release/0586779a-1e0c-465a-ad7d-5dd1c0946028</c>.</param>
    /// <param name="releaseId">The parsed MBID, if this returns <see langword="true"/>; <see cref="string.Empty"/> otherwise.</param>
    /// <returns><see langword="true"/> if <paramref name="url"/> is a well-formed absolute URL whose path is <c>release/&lt;guid&gt;</c>.</returns>
    internal static bool TryParseMusicBrainzReleaseId(string url, out string releaseId)
    {
        releaseId = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length != 2 || !segments[0].Equals("release", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(segments[1], out _))
        {
            return false;
        }

        releaseId = segments[1];
        return true;
    }

    /// <summary>Parses the release ID out of a Discogs release URL. <see langword="internal"/> as a test seam (pure parsing logic, no console I/O).</summary>
    /// <param name="url">The pasted URL, e.g. <c>https://www.discogs.com/release/249276-Bob-Dylan-Desire</c>.</param>
    /// <param name="releaseId">The parsed numeric ID, if this returns <see langword="true"/>; <see cref="string.Empty"/> otherwise.</param>
    /// <returns><see langword="true"/> if <paramref name="url"/> is a well-formed absolute URL whose <c>release/</c> path segment starts with digits.</returns>
    internal static bool TryParseDiscogsReleaseId(string url, out string releaseId)
    {
        releaseId = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length != 2 || !segments[0].Equals("release", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var digits = new string(segments[1].TakeWhile(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return false;
        }

        releaseId = digits;
        return true;
    }

    /// <summary>Fetches a release's full metadata and checks its track count against the disc before accepting it.</summary>
    /// <param name="service">The MusicBrainz metadata service to resolve through.</param>
    /// <param name="releaseId">The MusicBrainz release MBID to resolve.</param>
    /// <param name="expectedTrackCount">The disc's actual audio track count.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The resolved release; <see langword="null"/> (with an error already printed) if its track count doesn't match.</returns>
    private static async Task<ReleaseInfo?> ResolveAndValidateTrackCountAsync(MetadataService service, string releaseId, int expectedTrackCount, CancellationToken cancellationToken)
    {
        var releaseInfo = await service.ResolveAsync(releaseId, cancellationToken).ConfigureAwait(false);
        var actualTrackCount = releaseInfo.Media.Sum(medium => medium.Tracks.Count);
        if (actualTrackCount != expectedTrackCount)
        {
            Console.Error.WriteLine(
                $"'{releaseInfo.Artist} - {releaseInfo.Title}' has {actualTrackCount} track(s), but this disc has {expectedTrackCount} audio track(s) -- pick a different release.");
            return null;
        }

        return releaseInfo;
    }

    /// <summary>
    /// Prompts on stdin for a MusicBrainz release URL (e.g.
    /// <c>https://musicbrainz.org/release/&lt;mbid&gt;</c>) and parses the MBID
    /// out of it, without fetching anything yet -- the caller resolves the
    /// returned placeholder candidate's <see cref="ReleaseCandidate.MusicBrainzReleaseId"/>
    /// the same way it would resolve a disc-ID-matched one.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>
    /// A placeholder <see cref="ReleaseCandidate"/> carrying only the parsed
    /// MBID; <see langword="null"/> if the input was blank/EOF or didn't look
    /// like a MusicBrainz release URL (already reported to stderr).
    /// </returns>
    private static async Task<ReleaseCandidate?> PromptManualMusicBrainzOverrideAsync(CancellationToken cancellationToken)
    {
        Console.Write("MusicBrainz release URL: ");
        var input = ConsoleInputSanitizer.Clean(await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false));
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (!TryParseMusicBrainzReleaseId(input, out var releaseId))
        {
            Console.Error.WriteLine($"'{input}' doesn't look like a MusicBrainz release URL (expected https://musicbrainz.org/release/<mbid>).");
            return null;
        }

        return new ReleaseCandidate(releaseId, Artist: "(manual override)", Title: "(manual override)", Date: null, Country: null, Barcode: null, CatalogNumber: null);
    }

    /// <summary>
    /// Best-effort Discogs enrichment: searches by barcode (if known) and,
    /// on zero or one match, uses it automatically; on multiple matches,
    /// prompts with a skip option (and a manual-URL override -- see
    /// <see cref="PromptManualDiscogsOverrideAsync"/>). A Discogs failure
    /// never fails the command -- it's caught and logged, and the release is
    /// returned unenriched, per <c>init.md</c>'s "shouldn't be a blocking
    /// issue" requirement for Discogs.
    /// </summary>
    /// <param name="releaseInfo">The MusicBrainz-resolved release to enrich.</param>
    /// <param name="httpClientFactory">The shared factory to resolve the Discogs <see cref="HttpClient"/> from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns><paramref name="releaseInfo"/>, with <see cref="ReleaseInfo.Discogs"/> populated if a match was found and selected.</returns>
    private static async Task<ReleaseInfo> EnrichWithDiscogsAsync(ReleaseInfo releaseInfo, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(releaseInfo.Barcode))
        {
            return releaseInfo;
        }

        var discogsClient = new DiscogsClient(httpClientFactory.CreateClient("discogs"));

        IReadOnlyList<DiscogsInfo> candidates;
        try
        {
            candidates = await discogsClient.SearchByBarcodeAsync(releaseInfo.Barcode, cancellationToken).ConfigureAwait(false);
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
            _ => await ConsolePicker.PromptForSelectionAsync(
                $"Found {candidates.Count} matching Discogs releases:",
                candidates,
                DescribeDiscogsCandidate,
                allowSkip: true,
                manualOverride: ct => PromptManualDiscogsOverrideAsync(discogsClient, ct),
                cancellationToken: cancellationToken).ConfigureAwait(false),
        };

        return chosen is null ? releaseInfo : releaseInfo with { Discogs = chosen };
    }

    /// <summary>
    /// Prompts on stdin for a Discogs release URL (e.g.
    /// <c>https://www.discogs.com/release/&lt;id&gt;-Some-Slug</c>), parses the
    /// release ID out of it, and fetches it directly. Unlike the MusicBrainz
    /// override, there is no track-count check here -- <see cref="DiscogsInfo"/>
    /// never carries a track listing in this app (Discogs is enrichment only,
    /// on top of the MusicBrainz-resolved tracklist), so there is nothing to
    /// validate it against.
    /// </summary>
    /// <param name="discogsClient">The Discogs client to fetch the release from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The fetched release; <see langword="null"/> if the input was blank/EOF, didn't look like a Discogs release URL, or the fetch failed (all already reported to stderr).</returns>
    private static async Task<DiscogsInfo?> PromptManualDiscogsOverrideAsync(IDiscogsClient discogsClient, CancellationToken cancellationToken)
    {
        Console.Write("Discogs release URL: ");
        var input = ConsoleInputSanitizer.Clean(await Console.In.ReadLineAsync(cancellationToken).ConfigureAwait(false));
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        if (!TryParseDiscogsReleaseId(input, out var releaseId))
        {
            Console.Error.WriteLine($"'{input}' doesn't look like a Discogs release URL (expected https://www.discogs.com/release/<id>-...).");
            return null;
        }

        try
        {
            return await discogsClient.GetReleaseAsync(releaseId, cancellationToken).ConfigureAwait(false);
        }
        catch (DiscogsException ex)
        {
            Console.Error.WriteLine($"Discogs lookup failed: {ex.Message}");
            return null;
        }
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
        $"{candidate.Artist} - {candidate.Title} ({candidate.Date ?? "?"}, {candidate.Country ?? "?"}) cat={candidate.CatalogNumber ?? "?"} barcode={candidate.Barcode ?? "?"}";

    /// <summary>Formats one line describing a Discogs candidate for the picker.</summary>
    /// <param name="candidate">The candidate to describe.</param>
    /// <returns>The description.</returns>
    private static string DescribeDiscogsCandidate(DiscogsInfo candidate) =>
        $"{candidate.Title} ({candidate.Country ?? "?"}, {candidate.Format ?? "?"}) label={candidate.Label ?? "?"} cat={candidate.CatalogNumber ?? "?"} id={candidate.Id}";
}
