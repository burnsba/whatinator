namespace Whatinator.Core.Naming;

/// <summary>Sanitizes strings for safe use as filesystem file/folder names.</summary>
public static class FileNameSanitizer
{
    /// <summary>The placeholder returned when sanitizing leaves nothing but whitespace/dots.</summary>
    private const string EmptyResultPlaceholder = "unknown";

    /// <summary>Characters forbidden (or awkward) in file/folder names on at least one common filesystem.</summary>
    private static readonly char[] ForbiddenCharacters = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Replaces filesystem-forbidden characters (and control characters) with
    /// <c>_</c>, then trims leading/trailing whitespace and trailing dots --
    /// both legal on Linux but problematic on Windows/SMB shares, which
    /// matters since release folders routinely get copied to a NAS. A result
    /// that is empty or all-blank after trimming (e.g. from an empty
    /// MusicBrainz title) is replaced with <see cref="EmptyResultPlaceholder"/>
    /// rather than returned as-is.
    /// </summary>
    /// <remarks>
    /// Windows reserved device names (<c>CON</c>, <c>PRN</c>, <c>NUL</c>,
    /// <c>AUX</c>, <c>COM1</c>-<c>COM9</c>, <c>LPT1</c>-<c>LPT9</c>) are
    /// deliberately not handled: every call site
    /// (<see cref="Whatinator.Core.Naming.ReleaseFolderNaming"/>,
    /// <see cref="Whatinator.Core.Naming.TrackFileNaming"/>) builds its input
    /// by concatenating multiple fields (artist, title, year, track number),
    /// so an exact collision with a reserved name is not practically
    /// reachable.
    /// </remarks>
    /// <param name="value">The raw string to sanitize.</param>
    /// <returns>A string safe to use as a single path component.</returns>
    public static string Sanitize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(ForbiddenCharacters, chars[i]) >= 0 || char.IsControl(chars[i]))
            {
                chars[i] = '_';
            }
        }

        var sanitized = new string(chars).Trim().TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(sanitized) ? EmptyResultPlaceholder : sanitized;
    }
}
