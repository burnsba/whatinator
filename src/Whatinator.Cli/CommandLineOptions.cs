namespace Whatinator.Cli;

/// <summary>Whether an <see cref="OptionSpec"/> takes a value or is presence-only.</summary>
internal enum OptionArity
{
    /// <summary>Presence-only, e.g. <c>--ask</c>.</summary>
    Flag,

    /// <summary>Takes a following value, e.g. <c>--device foo</c>.</summary>
    RequiresValue,
}

/// <summary>One option a command declares it accepts, for <see cref="ParsedOptions.Parse"/>.</summary>
/// <param name="LongName">The long option name, including leading dashes (e.g. <c>--device</c>).</param>
/// <param name="ShortName">The short option name, including leading dash (e.g. <c>-d</c>), or <see langword="null"/> if none.</param>
/// <param name="Arity">Whether the option takes a value or is presence-only.</param>
internal readonly record struct OptionSpec(string LongName, string? ShortName, OptionArity Arity)
{
    /// <summary>Declares a presence-only option.</summary>
    /// <param name="longName">The long option name, including leading dashes.</param>
    /// <param name="shortName">The short option name, including leading dash, or <see langword="null"/> if none.</param>
    /// <returns>The spec.</returns>
    public static OptionSpec Flag(string longName, string? shortName = null) => new(longName, shortName, OptionArity.Flag);

    /// <summary>Declares an option that requires a following value.</summary>
    /// <param name="longName">The long option name, including leading dashes.</param>
    /// <param name="shortName">The short option name, including leading dash, or <see langword="null"/> if none.</param>
    /// <returns>The spec.</returns>
    public static OptionSpec Value(string longName, string? shortName = null) => new(longName, shortName, OptionArity.RequiresValue);
}

/// <summary>
/// The result of validating a command's <c>args</c> against the options it
/// declares it understands. Replaces the old scan-only <c>GetValue</c>/
/// <c>HasFlag</c> pair, which never noticed an unknown/misspelled option, a
/// missing value, a value that was actually the next flag, or a duplicated
/// option (see <c>docs/backlog-completed/006-unknown-options-silently-ignored.md</c>).
/// </summary>
internal sealed class ParsedOptions
{
    /// <summary>
    /// <c>--debug</c> is consumed by <c>Program.IsDebugEnabled</c> before
    /// dispatch and is valid on every command; it's folded into every parse
    /// implicitly rather than requiring each command to redeclare it.
    /// </summary>
    private static readonly OptionSpec DebugSpec = OptionSpec.Flag("--debug");

    private readonly Dictionary<string, string> values;
    private readonly HashSet<string> flags;

    private ParsedOptions(Dictionary<string, string> values, HashSet<string> flags, IReadOnlyList<string> errors)
    {
        this.values = values;
        this.flags = flags;
        this.Errors = errors;
    }

    /// <summary>The errors found while parsing, in the order encountered. Empty if <c>args</c> was valid.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary><see langword="true"/> if any error was found -- the caller should print <see cref="Errors"/> to stderr and return exit code 1.</summary>
    public bool HasErrors => this.Errors.Count > 0;

    /// <summary>
    /// Parses <paramref name="args"/> against the given specs. Every token is
    /// accounted for: a recognized flag or value option is consumed, an
    /// unrecognized <c>-</c>-prefixed token or a stray non-option token
    /// produces an error, a value-taking option with no following token (or
    /// one that itself looks like an option) produces an error, and a
    /// duplicated option -- flag or value -- produces an error rather than
    /// silently keeping the first or last occurrence.
    /// </summary>
    /// <param name="args">The arguments to parse (not including the command name).</param>
    /// <param name="specs">The options this command understands. <c>--debug</c> is always implicitly understood.</param>
    /// <returns>The parsed result; check <see cref="HasErrors"/> before using <see cref="GetValue"/>/<see cref="HasFlag"/>.</returns>
    public static ParsedOptions Parse(string[] args, params OptionSpec[] specs)
    {
        var byName = new Dictionary<string, OptionSpec>();
        foreach (var spec in specs.Append(DebugSpec))
        {
            byName[spec.LongName] = spec;
            if (spec.ShortName is not null)
            {
                byName[spec.ShortName] = spec;
            }
        }

        var values = new Dictionary<string, string>();
        var flags = new HashSet<string>();
        var errors = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];

            if (!byName.TryGetValue(token, out var spec))
            {
                errors.Add(token.StartsWith('-') ? $"Unknown option: {token}" : $"Unexpected argument: {token}");
                continue;
            }

            if (spec.Arity == OptionArity.Flag)
            {
                if (!flags.Add(spec.LongName))
                {
                    errors.Add($"{spec.LongName} given more than once.");
                }

                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
            {
                errors.Add($"{spec.LongName} requires a value.");
                continue;
            }

            if (!values.TryAdd(spec.LongName, args[i + 1]))
            {
                errors.Add($"{spec.LongName} given more than once.");
            }

            i++;
        }

        return new ParsedOptions(values, flags, errors);
    }

    /// <summary>Gets a value-taking option's value.</summary>
    /// <param name="longName">The option's long name, as declared in the spec passed to <see cref="Parse"/>.</param>
    /// <returns>The value, or <see langword="null"/> if the option wasn't given.</returns>
    public string? GetValue(string longName) => this.values.TryGetValue(longName, out var value) ? value : null;

    /// <summary>Checks whether a presence-only option was given.</summary>
    /// <param name="longName">The option's long name, as declared in the spec passed to <see cref="Parse"/>.</param>
    /// <returns><see langword="true"/> if the option was given.</returns>
    public bool HasFlag(string longName) => this.flags.Contains(longName);
}
