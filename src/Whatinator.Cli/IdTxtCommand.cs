using Whatinator.Core;

namespace Whatinator.Cli;

/// <summary>Implements the <c>id-txt</c> command.</summary>
internal static class IdTxtCommand
{
    /// <summary>Generates <c>id.txt</c> from a saved <c>releaseinfo.json</c>.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] args)
    {
        var options = ParsedOptions.Parse(args, OptionSpec.Value("--releaseinfo"), OptionSpec.Value("--dest"));
        if (options.HasErrors)
        {
            foreach (var error in options.Errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        var releaseInfoPath = options.GetValue("--releaseinfo");
        if (releaseInfoPath is null)
        {
            Console.Error.WriteLine("id-txt requires --releaseinfo <path>.");
            return 1;
        }

        var dest = options.GetValue("--dest") ?? ".";

        if (!CliArgumentParsing.TryLoadReleaseInfo(releaseInfoPath, out var releaseInfo, out var loadError))
        {
            Console.Error.WriteLine(loadError);
            return 1;
        }

        var outputPath = Path.Combine(dest, "id.txt");
        try
        {
            Directory.CreateDirectory(dest);
            IdTextFile.Write(releaseInfo, outputPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Failed to write to {dest}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Wrote {outputPath}");
        return 0;
    }
}
