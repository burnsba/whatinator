using Whatinator.Core.Drive;

namespace Whatinator.Cli;

/// <summary>Implements the <c>list-device</c> command.</summary>
internal static class ListDeviceCommand
{
    /// <summary>Enumerates and prints optical drives found on the system.</summary>
    /// <param name="args">Remaining arguments after the command name. This command takes none.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] args)
    {
        var options = ParsedOptions.Parse(args);
        if (options.HasErrors)
        {
            foreach (var error in options.Errors)
            {
                Console.Error.WriteLine(error);
            }

            return 1;
        }

        var drives = OpticalDriveLocator.Enumerate();
        if (drives.Count == 0)
        {
            Console.WriteLine("No optical drives found.");
            return 0;
        }

        foreach (var drive in drives)
        {
            var label = string.Join(
                ' ',
                new[] { drive.Vendor, drive.Model }.Where(s => !string.IsNullOrWhiteSpace(s)));
            Console.WriteLine(label.Length > 0 ? $"{drive.DevicePath}  {label}" : drive.DevicePath);
        }

        return 0;
    }
}
