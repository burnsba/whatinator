namespace Whatinator.Cli;

/// <summary>Shared command-line option parsing helpers used across commands.</summary>
internal static class CommandLineOptions
{
    /// <summary>Extracts a named option's value, e.g. <c>--device foo</c> or <c>-d foo</c>.</summary>
    /// <param name="args">The arguments to scan.</param>
    /// <param name="longName">The long option name, including leading dashes (e.g. <c>--device</c>).</param>
    /// <param name="shortName">The short option name, including leading dash (e.g. <c>-d</c>), or <see langword="null"/> if none.</param>
    /// <returns>The option's value, or <see langword="null"/> if not present.</returns>
    public static string? GetValue(string[] args, string longName, string? shortName = null)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if ((args[i] == longName || (shortName is not null && args[i] == shortName)) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>Checks whether a presence-only flag (e.g. <c>--no-flac</c>, no value) was given.</summary>
    /// <param name="args">The arguments to scan.</param>
    /// <param name="name">The flag name, including leading dashes.</param>
    /// <returns><see langword="true"/> if <paramref name="name"/> appears anywhere in <paramref name="args"/>.</returns>
    public static bool HasFlag(string[] args, string name) => args.Contains(name);
}
