using System.ComponentModel;
using Whatinator.Core;
using Whatinator.Core.Drive;

namespace Whatinator.Cli;

/// <summary>Implements the <c>cache-check</c> command.</summary>
internal static class CacheCheckCommand
{
    /// <summary>
    /// Runs <see cref="CacheDefeatAnalyzer.AnalyzeAsync"/> against the disc
    /// currently inserted, saving the classification to the per-drive config
    /// map -- same shape as <see cref="OffsetFindCommand"/>'s write to
    /// <see cref="WhatinatorConfig.ReadOffsets"/>.
    /// </summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = ParsedOptions.Parse(args, OptionSpec.Value("--device"));
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

        Console.WriteLine($"Running cd-paranoia -A against {device} -- this performs a full read/timing pass over the disc and can take several minutes.");

        CacheDefeatResult result;
        try
        {
            result = await CacheDefeatAnalyzer.AnalyzeAsync(device, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or Win32Exception)
        {
            Console.Error.WriteLine($"Failed to read {device}: {ex.Message}");
            return 1;
        }

        var drive = context.ResolveDrive();
        var key = WhatinatorConfig.DriveKey(drive?.Vendor, drive?.Model, drive?.Release);
        var updatedCacheDefeats = new Dictionary<string, CacheDefeatResult>(config.CacheDefeats ?? new Dictionary<string, CacheDefeatResult>())
        {
            [key] = result,
        };
        var configPath = ConfigLoader.ResolveDefaultPath();
        ConfigLoader.Save(config with { CacheDefeats = updatedCacheDefeats }, configPath);

        Console.WriteLine();
        Console.WriteLine($"Result: {result}");
        Console.WriteLine($"Saved to {configPath} under drive key '{key}' (overwriting any prior entry).");

        return 0;
    }
}
