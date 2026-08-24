using Whatinator.Core;
using Whatinator.Core.CoverArt;
using Whatinator.Core.Flac;

namespace Whatinator.Cli;

/// <summary>Implements the <c>flac</c> command.</summary>
internal static class FlacCommand
{
    /// <summary>Packages a rip's FLAC output into the project's standard folder layout.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="httpClientFactory">The shared factory to resolve the Cover Art Archive <see cref="HttpClient"/> from.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken = default)
    {
        var releaseInfoPath = CommandLineOptions.GetValue(args, "--releaseinfo");
        var source = CommandLineOptions.GetValue(args, "--source");
        if (releaseInfoPath is null || source is null)
        {
            Console.Error.WriteLine("flac requires --releaseinfo <path> and --source <path>.");
            return 1;
        }

        var dest = CommandLineOptions.GetValue(args, "--dest") ?? ".";

        if (!CliArgumentParsing.TryParseDiscNumber(CommandLineOptions.GetValue(args, "--disc"), out var discError, out var discNumber))
        {
            Console.Error.WriteLine(discError);
            return 1;
        }

        if (!CliArgumentParsing.TryLoadReleaseInfo(releaseInfoPath, out var releaseInfo, out var loadError))
        {
            Console.Error.WriteLine(loadError);
            return 1;
        }

        var config = ConfigLoader.Load();
        var coverArtClient = new CoverArtClient(config.EffectiveUserAgent, httpClientFactory.CreateClient("coverart"));
        var packager = new FlacPackager(coverArtClient);

        FlacPackageResult result;
        try
        {
            result = await packager
                .PackageAsync(new FlacPackageOptions(releaseInfo, source, dest, discNumber), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DirectoryNotFoundException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine($"Packaged {result.MovedFlacFileCount} track(s) into {result.DiscDirectory}");
        if (result.LogFilePath is not null)
        {
            Console.WriteLine($"Log: {result.LogFilePath}");
        }

        if (result.CoverArtPath is not null)
        {
            Console.WriteLine($"Cover art: {result.CoverArtPath}");
        }

        Console.WriteLine($"Release folder: {result.ContainerDirectory}");
        return 0;
    }
}
