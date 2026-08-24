using Whatinator.Core;
using Whatinator.Core.Metadata;
using Whatinator.Core.MusicBrainz;
using Whatinator.LibDiscId;

namespace Whatinator.Cli;

/// <summary>Implements the <c>disc-info</c> command.</summary>
internal static class DiscInfoCommand
{
    /// <summary>
    /// Reads a disc's TOC and, best-effort, its MusicBrainz artist/title/track listing.
    /// Uses <c>libdiscid</c> for a fast disc-identification read -- for the
    /// frame-accurate technical TOC that <c>rip</c>/<c>pipeline</c> use
    /// internally (via <c>cdrdao</c>), see <c>toc</c> instead.
    /// </summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="httpClientFactory">The shared factory to resolve the MusicBrainz <see cref="HttpClient"/> from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken = default)
    {
        var options = ParsedOptions.Parse(args, OptionSpec.Value("--device", "-d"), OptionSpec.Flag("--ask"));
        if (options.HasErrors)
        {
            foreach (var error in options.Errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        var context = CommandContext.Resolve(options);
        var config = context.Config;
        var device = context.Device;
        var ask = options.HasFlag("--ask");

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
            return 1;
        }

        Console.WriteLine($"Device: {device}");
        DiscInfoFormatter.Print(disc);

        var musicBrainzClient = new MusicBrainzClient(config.EffectiveUserAgent, httpClientFactory.CreateClient("musicbrainz"));
        var service = new MetadataService(musicBrainzClient);

        ReleaseInfo releaseInfo;
        try
        {
            var result = await service.LookupByDiscIdAsync(disc.Id, cancellationToken).ConfigureAwait(false);
            switch (result.Status)
            {
                case MetadataLookupStatus.Found:
                    releaseInfo = result.ReleaseInfo!;
                    break;
                case MetadataLookupStatus.Ambiguous:
                    var candidates = result.Candidates!;
                    ReleaseCandidate chosen;
                    if (ask)
                    {
                        var picked = ConsolePicker.PromptForSelection(
                            $"Found {candidates.Count} matching releases:",
                            candidates,
                            DescribeCandidate,
                            allowSkip: false);
                        if (picked is null)
                        {
                            // Matches MakeReleaseInfoCommand's identical "no
                            // selection made" path -- see root CLAUDE.md and
                            // docs/backlog-completed/017-disc-info-ask-exit-code-inconsistent.md.
                            Console.Error.WriteLine("No selection made.");
                            return 1;
                        }

                        chosen = picked;
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Note: {candidates.Count} releases matched this disc; using the first. Others (rerun with --ask to pick):");
                        foreach (var other in candidates.Skip(1))
                        {
                            Console.WriteLine($"  - {DescribeCandidate(other)}");
                        }

                        chosen = candidates[0];
                    }

                    releaseInfo = await service.ResolveAsync(chosen.MusicBrainzReleaseId, cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    Console.WriteLine();
                    Console.WriteLine("No MusicBrainz release matched this disc.");
                    return 0;
            }
        }
        catch (MusicBrainzException ex)
        {
            Console.Error.WriteLine($"MusicBrainz lookup failed: {ex.Message}");
            return 0;
        }

        Console.WriteLine();
        DiscInfoFormatter.PrintRelease(releaseInfo);
        return 0;
    }

    /// <summary>Formats one line describing a MusicBrainz candidate for the picker/note listing.</summary>
    /// <param name="candidate">The candidate to describe.</param>
    /// <returns>The description.</returns>
    private static string DescribeCandidate(ReleaseCandidate candidate) =>
        $"{candidate.Artist} - {candidate.Title} ({candidate.Date ?? "?"}, {candidate.Country ?? "?"})";
}
