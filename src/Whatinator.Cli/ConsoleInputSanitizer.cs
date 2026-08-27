namespace Whatinator.Cli;

/// <summary>Cleans up raw stdin lines before they're parsed as a selection or a pasted URL.</summary>
internal static class ConsoleInputSanitizer
{
    /// <summary>
    /// Strips non-printable/control characters from <paramref name="input"/>
    /// and trims surrounding whitespace. A terminal that doesn't honor
    /// bracketed paste can turn a paste keystroke (e.g. Ctrl-V, which some
    /// terminals send as a raw SYN control byte instead of pasting) into a
    /// stray control character landing in an otherwise-legitimate line --
    /// left in, that byte makes an exact string/number comparison
    /// (<c>"m"</c>, a track count, an MBID) fail for a reason invisible to
    /// whoever's looking at the prompt.
    /// </summary>
    /// <param name="input">The raw line read from stdin, or <see langword="null"/>.</param>
    /// <returns>The cleaned line, or <see langword="null"/> if <paramref name="input"/> was <see langword="null"/>.</returns>
    public static string? Clean(string? input)
    {
        if (input is null)
        {
            return null;
        }

        var withoutControlChars = new string(input.Where(c => !char.IsControl(c)).ToArray());
        return withoutControlChars.Trim();
    }
}
