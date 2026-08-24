namespace Whatinator.Cli;

/// <summary>Shared interactive candidate-selection prompt, used by any command that needs to disambiguate multiple matches on stdin.</summary>
internal static class ConsolePicker
{
    /// <summary>Prints numbered candidates and prompts on stdin until a valid selection (or skip) is made.</summary>
    /// <typeparam name="T">The candidate type.</typeparam>
    /// <param name="header">A line printed before the candidate list.</param>
    /// <param name="candidates">The candidates to choose from.</param>
    /// <param name="describe">Formats one candidate for display.</param>
    /// <param name="allowSkip">Whether to offer a <c>[0]</c> "skip" option.</param>
    /// <param name="isOutputRedirected">
    /// Overrides the <see cref="Console.IsOutputRedirected"/> check, for
    /// tests. Production callers omit this and get the real console state.
    /// </param>
    /// <returns>
    /// The chosen candidate; <see langword="null"/> if the user skipped
    /// (only possible when <paramref name="allowSkip"/> is
    /// <see langword="true"/>), stdin closed (EOF) before a valid selection
    /// was made, or stdout is redirected (prompting would either hang
    /// silently or pollute the redirected output, so this fails fast
    /// instead of trying).
    /// </returns>
    public static T? PromptForSelection<T>(string header, IReadOnlyList<T> candidates, Func<T, string> describe, bool allowSkip, bool? isOutputRedirected = null)
        where T : class
    {
        if (isOutputRedirected ?? Console.IsOutputRedirected)
        {
            Console.Error.WriteLine($"{candidates.Count} candidates matched, but stdout is redirected -- cannot prompt interactively. Rerun without redirecting output to choose one.");
            return null;
        }

        Console.WriteLine(header);
        for (var i = 0; i < candidates.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {describe(candidates[i])}");
        }

        if (allowSkip)
        {
            Console.WriteLine("  [0] Skip / none of these");
        }

        var minChoice = allowSkip ? 0 : 1;
        while (true)
        {
            Console.Write($"Select [{minChoice}-{candidates.Count}]: ");
            var input = Console.ReadLine();
            if (input is null)
            {
                // stdin closed (EOF/redirected empty input) -- stop prompting rather than
                // spin forever re-reading null.
                return null;
            }

            if (int.TryParse(input, out var choice) && choice >= minChoice && choice <= candidates.Count)
            {
                return choice == 0 ? null : candidates[choice - 1];
            }

            Console.WriteLine("Invalid selection, try again.");
        }
    }
}
