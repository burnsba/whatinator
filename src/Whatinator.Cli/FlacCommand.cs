using System.Text.Json;
using Whatinator.Core;
using Whatinator.Core.CoverArt;
using Whatinator.Core.Flac;
using Whatinator.Core.Metadata;

namespace Whatinator.Cli;

/// <summary>Implements the <c>flac</c> command.</summary>
internal static class FlacCommand
{
    /// <summary>The <c>User-Agent</c> sent with every Cover Art Archive request.</summary>
    private const string UserAgent = "whatinator/0.1 ( bethany.whatinator@burnsba.net )";

    /// <summary>Packages a rip's FLAC output into the project's standard folder layout.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="httpClientFactory">The shared factory to resolve the Cover Art Archive <see cref="HttpClient"/> from.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory)
    {
        var releaseInfoPath = CommandLineOptions.GetValue(args, "--releaseinfo");
        var source = CommandLineOptions.GetValue(args, "--source");
        if (releaseInfoPath is null || source is null)
        {
            Console.Error.WriteLine("flac requires --releaseinfo <path> and --source <path>.");
            return 1;
        }

        var dest = CommandLineOptions.GetValue(args, "--dest") ?? ".";

        int? discNumber = null;
        var discArg = CommandLineOptions.GetValue(args, "--disc");
        if (discArg is not null)
        {
            if (!int.TryParse(discArg, out var parsed))
            {
                Console.Error.WriteLine($"--disc must be a number, got '{discArg}'.");
                return 1;
            }

            discNumber = parsed;
        }

        ReleaseInfo releaseInfo;
        try
        {
            releaseInfo = ReleaseInfoFile.Load(releaseInfoPath);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Console.Error.WriteLine($"Failed to read {releaseInfoPath}: {ex.Message}");
            return 1;
        }

        var coverArtClient = new CoverArtClient(UserAgent, httpClientFactory.CreateClient("coverart"));
        var packager = new FlacPackager(coverArtClient);

        FlacPackageResult result;
        try
        {
            result = await packager
                .PackageAsync(new FlacPackageOptions(releaseInfo, source, dest, discNumber))
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
