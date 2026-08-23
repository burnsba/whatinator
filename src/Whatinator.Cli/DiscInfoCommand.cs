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
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory)
    {
        var config = ConfigLoader.Load();
        var device = CommandLineOptions.GetValue(args, "--device", "-d") ?? config.Device;
        var ask = CommandLineOptions.HasFlag(args, "--ask");

        Disc disc;
        try
        {
            disc = DiscReader.Read(device);
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
            var result = await service.LookupByDiscIdAsync(disc.Id).ConfigureAwait(false);
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
                            Console.Error.WriteLine("No selection made.");
                            return 0;
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

                    releaseInfo = await service.ResolveAsync(chosen.MusicBrainzReleaseId).ConfigureAwait(false);
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
