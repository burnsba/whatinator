using System.Text.Json;
using Whatinator.Core;
using Whatinator.Core.Metadata;

namespace Whatinator.Cli;

/// <summary>Implements the <c>update-metadata</c> command.</summary>
internal static class UpdateMetadataCommand
{
    /// <summary>Applies a corrected <c>releaseinfo.json</c> to an already-packaged release folder.</summary>
    /// <param name="args">Remaining arguments after the command name.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] args)
    {
        var releaseInfoPath = CommandLineOptions.GetValue(args, "--releaseinfo");
        var dest = CommandLineOptions.GetValue(args, "--dest");
        if (releaseInfoPath is null || dest is null)
        {
            Console.Error.WriteLine("update-metadata requires --releaseinfo <path> and --dest <path>.");
            return 1;
        }

        ReleaseInfo newReleaseInfo;
        ReleaseInfo oldReleaseInfo;
        try
        {
            newReleaseInfo = ReleaseInfoFile.Load(releaseInfoPath);
            oldReleaseInfo = ReleaseInfoFile.Load(Path.Combine(dest, "releaseinfo.json"));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            Console.Error.WriteLine($"Failed to read metadata: {ex.Message}");
            return 1;
        }

        var change = MetadataUpdater.DetectChange(oldReleaseInfo, newReleaseInfo);
        if (change.ArtistOrTitleChanged)
        {
            Console.WriteLine($"Artist: '{change.OldArtist}' -> '{change.NewArtist}'");
            Console.WriteLine($"Title:  '{change.OldTitle}' -> '{change.NewTitle}'");
            Console.Write("Artist and/or title differ from what's currently in this folder. Proceed? [y/N]: ");
            var input = Console.ReadLine();
            if (input is null || !input.Trim().Equals("y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted.");
                return 1;
            }
        }

        MetadataUpdateResult result;
        try
        {
            result = MetadataUpdater.Apply(newReleaseInfo, dest);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine($"Backed up previous metadata to {result.BackupPath}");
        Console.WriteLine($"Recalculated checksums for {result.ChecksumFileCount} file(s): {result.ChecksumFilePath}");
        if (result.FolderRenamed)
        {
            Console.WriteLine($"Renamed folder to {result.FinalDirectory}");
        }

        Console.WriteLine($"Updated {result.FinalDirectory}");
        return 0;
    }
}
