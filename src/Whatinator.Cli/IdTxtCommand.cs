using System.Text.Json;
using Whatinator.Core;
using Whatinator.Core.Metadata;

namespace Whatinator.Cli;

/// <summary>Implements the <c>id-txt</c> command.</summary>
internal static class IdTxtCommand
{
    /// <summary>Generates <c>id.txt</c> from a saved <c>releaseinfo.json</c>.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] args)
    {
        var releaseInfoPath = CommandLineOptions.GetValue(args, "--releaseinfo");
        if (releaseInfoPath is null)
        {
            Console.Error.WriteLine("id-txt requires --releaseinfo <path>.");
            return 1;
        }

        var dest = CommandLineOptions.GetValue(args, "--dest") ?? ".";

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

        Directory.CreateDirectory(dest);
        var outputPath = Path.Combine(dest, "id.txt");
        IdTextFile.Write(releaseInfo, outputPath);

        Console.WriteLine($"Wrote {outputPath}");
        return 0;
    }
}
