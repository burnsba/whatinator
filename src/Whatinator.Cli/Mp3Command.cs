using System.Text.Json;
using Whatinator.Core;
using Whatinator.Core.Metadata;
using Whatinator.Core.Mp3;

namespace Whatinator.Cli;

/// <summary>Implements the <c>mp3</c> command.</summary>
internal static class Mp3Command
{
    /// <summary>Encodes a FLAC folder into the project's standard V0 MP3 folder layout.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        var releaseInfoPath = CommandLineOptions.GetValue(args, "--releaseinfo");
        var source = CommandLineOptions.GetValue(args, "--source");
        if (releaseInfoPath is null || source is null)
        {
            Console.Error.WriteLine("mp3 requires --releaseinfo <path> and --source <path>.");
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

        var packager = new Mp3Packager();

        Mp3PackageResult result;
        try
        {
            result = await packager
                .PackageAsync(
                    new Mp3PackageOptions(releaseInfo, source, dest, discNumber),
                    Console.OpenStandardOutput(),
                    Console.OpenStandardError())
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DirectoryNotFoundException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine($"Encoded {result.EncodedTrackCount} track(s) into {result.DiscDirectory}");
        Console.WriteLine($"Log: {result.LogFilePath}");
        if (result.CoverArtPath is not null)
        {
            Console.WriteLine($"Cover art: {result.CoverArtPath}");
        }

        Console.WriteLine($"Release folder: {result.ContainerDirectory}");
        return 0;
    }
}
