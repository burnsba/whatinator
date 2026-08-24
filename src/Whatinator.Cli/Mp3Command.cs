using Whatinator.Core;
using Whatinator.Core.Mp3;

namespace Whatinator.Cli;

/// <summary>Implements the <c>mp3</c> command.</summary>
internal static class Mp3Command
{
    /// <summary>Encodes a FLAC folder into the project's standard V0 MP3 folder layout.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <param name="cancellationToken">Cancelled when the user hits Ctrl-C.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var options = ParsedOptions.Parse(
            args,
            OptionSpec.Value("--releaseinfo"),
            OptionSpec.Value("--source"),
            OptionSpec.Value("--dest"),
            OptionSpec.Value("--disc"));
        if (options.HasErrors)
        {
            foreach (var error in options.Errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        var releaseInfoPath = options.GetValue("--releaseinfo");
        var source = options.GetValue("--source");
        if (releaseInfoPath is null || source is null)
        {
            Console.Error.WriteLine("mp3 requires --releaseinfo <path> and --source <path>.");
            return 1;
        }

        var dest = options.GetValue("--dest") ?? ".";

        if (!CliArgumentParsing.TryParseDiscNumber(options.GetValue("--disc"), out var discError, out var discNumber))
        {
            Console.Error.WriteLine(discError);
            return 1;
        }

        if (!CliArgumentParsing.TryLoadReleaseInfo(releaseInfoPath, out var releaseInfo, out var loadError))
        {
            Console.Error.WriteLine(loadError);
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
                    Console.OpenStandardError(),
                    cancellationToken)
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
