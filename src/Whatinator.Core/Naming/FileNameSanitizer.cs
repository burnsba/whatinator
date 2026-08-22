namespace Whatinator.Core.Naming;

/// <summary>Sanitizes strings for safe use as filesystem file/folder names.</summary>
public static class FileNameSanitizer
{
    /// <summary>Characters forbidden (or awkward) in file/folder names on at least one common filesystem.</summary>
    private static readonly char[] ForbiddenCharacters = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>Replaces filesystem-forbidden characters (and control characters) with <c>_</c>.</summary>
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

        return new string(chars);
    }
}
