using Whatinator.Core.Drive;

namespace Whatinator.Cli;

/// <summary>Implements the <c>list-device</c> command.</summary>
internal static class ListDeviceCommand
{
    /// <summary>Enumerates and prints optical drives found on the system.</summary>
    /// <returns>The process exit code.</returns>
    public static int Run()
    {
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
