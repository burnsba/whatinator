using System.Text.RegularExpressions;

namespace Whatinator.Core.Drive;

/// <summary>Enumerates optical drives visible to the system via sysfs.</summary>
public static partial class OpticalDriveLocator
{
    /// <summary>
    /// Enumerates optical (<c>/dev/sr*</c>) drives by reading
    /// <paramref name="sysClassBlockPath"/>, ordered by device path.
    /// </summary>
    /// <param name="sysClassBlockPath">
    /// The sysfs block class directory to scan. Defaults to
    /// <c>/sys/class/block</c>; overridable for testing.
    /// </param>
    /// <returns>Every optical drive found. Vendor/model/release are populated when readable.</returns>
    public static IReadOnlyList<OpticalDrive> Enumerate(string sysClassBlockPath = "/sys/class/block")
    {
        if (!Directory.Exists(sysClassBlockPath))
        {
            return Array.Empty<OpticalDrive>();
        }

        var drives = new List<OpticalDrive>();
        foreach (var dir in Directory.EnumerateDirectories(sysClassBlockPath))
        {
            var name = Path.GetFileName(dir);
            if (!OpticalDriveNamePattern().IsMatch(name))
            {
                continue;
            }

            var vendor = ReadTrimmed(Path.Combine(dir, "device", "vendor"));
            var model = ReadTrimmed(Path.Combine(dir, "device", "model"));
            var release = ReadTrimmed(Path.Combine(dir, "device", "rev"));
            drives.Add(new OpticalDrive($"/dev/{name}", vendor, model, release));
        }

        return drives.OrderBy(drive => drive.DevicePath, StringComparer.Ordinal).ToList();
    }

    /// <summary>Reads and trims a sysfs attribute file, returning <see langword="null"/> if it can't be read.</summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The trimmed file contents, or <see langword="null"/> on any read failure.</returns>
    private static string? ReadTrimmed(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Matches sysfs block device names for optical drives, e.g. <c>sr0</c>, <c>sr1</c>.</summary>
    /// <returns>A compiled regex matching <c>sr</c> followed by one or more digits.</returns>
    [GeneratedRegex(@"^sr\d+$")]
    private static partial Regex OpticalDriveNamePattern();
}
