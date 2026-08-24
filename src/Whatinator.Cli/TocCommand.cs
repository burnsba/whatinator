using System.ComponentModel;
using Whatinator.Core.Toc;

namespace Whatinator.Cli;

/// <summary>Implements the <c>toc</c> command.</summary>
internal static class TocCommand
{
    /// <summary>
    /// Reads and prints a disc's frame-accurate table of contents via
    /// <c>cdrdao read-toc</c> -- the same technical read <c>rip</c>/
    /// <c>pipeline</c> use internally before ripping. For a human-facing
    /// MusicBrainz disc-identification lookup (artist/title/track listing)
    /// instead, see <c>disc-info</c>.
    /// </summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = ParsedOptions.Parse(args, OptionSpec.Value("--device", "-d"), OptionSpec.Flag("--full"));
        if (options.HasErrors)
        {
            foreach (var error in options.Errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        var device = CommandContext.Resolve(options).Device;
        var full = options.HasFlag("--full");

        var reader = new CdrdaoTocReader();
        DiscToc toc;
        try
        {
            toc = await reader.ReadAsync(device, fastToc: !full, Console.OpenStandardError(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or Win32Exception)
        {
            Console.Error.WriteLine($"Failed to read TOC from {device}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Device: {device}");
        TocFormatter.Print(toc);

        return 0;
    }
}
