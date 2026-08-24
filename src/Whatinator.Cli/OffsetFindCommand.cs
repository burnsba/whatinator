using System.ComponentModel;
using Whatinator.Core;
using Whatinator.Core.AccurateRip;
using Whatinator.Core.Drive;

namespace Whatinator.Cli;

/// <summary>Implements the <c>offset-find</c> command.</summary>
internal static class OffsetFindCommand
{
    /// <summary>
    /// Auto-detects the drive's sample read offset against the disc
    /// currently inserted, saving the result to the per-drive config map on
    /// success.
    /// </summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="httpClientFactory">The shared factory to resolve the AccurateRip database <see cref="HttpClient"/> from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken = default)
    {
        var options = ParsedOptions.Parse(args, OptionSpec.Value("--device", "-d"));
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

        // Deliberately not disposed: wraps the process's real stdout, which
        // Console.Write* below still needs to use -- same pattern as rip/mp3.
        var standardOutput = Console.OpenStandardOutput();

        var accurateRipClient = new AccurateRipClient(config.EffectiveUserAgent, httpClientFactory.CreateClient("accuraterip"));
        var finder = new OffsetFinder(accurateRipClient);

        OffsetFindResult result;
        try
        {
            result = await finder.FindAsync(device, standardOutput, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or Win32Exception)
        {
            Console.Error.WriteLine($"Failed to read {device}: {ex.Message}");
            return 1;
        }

        Console.WriteLine();

        if (!result.Found)
        {
            Console.Error.WriteLine(result.FailureReason switch
            {
                OffsetFindFailureReason.TooFewTracks =>
                    "This disc has fewer than 3 audio tracks -- offset-find needs at least 3 to do its job. Try a different disc.",
                OffsetFindFailureReason.NoAccurateRipEntry =>
                    "AccurateRip entry not found: drive offset can't be determined from this disc. Try a different disc.",
                OffsetFindFailureReason.NoOffsetMatched =>
                    "None of the candidate offsets fully matched this disc. Try a different disc.",
                _ => "Offset detection failed.",
            });
            return 1;
        }

        var drive = context.ResolveDrive();
        var key = WhatinatorConfig.DriveKey(drive?.Vendor, drive?.Model, drive?.Release);
        var updatedOffsets = new Dictionary<string, int>(config.ReadOffsets ?? new Dictionary<string, int>())
        {
            [key] = result.Offset!.Value,
        };
        var configPath = ConfigLoader.ResolveDefaultPath();
        ConfigLoader.Save(config with { ReadOffsets = updatedOffsets }, configPath);

        Console.WriteLine($"Found offset: {result.Offset}");
        Console.WriteLine($"Saved to {configPath} under drive key '{key}'.");

        return 0;
    }
}
