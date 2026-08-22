using System.Text;

namespace Whatinator.Cli;

/// <summary>
/// Renders <see cref="HelpContent.Sections"/> as <c>whatinator --help</c>'s
/// two-column output: a fixed-width left column for each command's usage
/// syntax, and a right column that word-wraps to use the whole detected
/// console width (minus a 10-character margin) -- recomputed on every call,
/// never cached, so a piped/redirected run and an interactive one can differ.
/// </summary>
internal static class HelpFormatter
{
    /// <summary>How far the right (description) column starts, in characters from the left edge -- matches the left column's historical width.</summary>
    private const int UsageColumnWidth = 35;

    /// <summary>How many characters of the detected console width to leave unused, per <c>docs/plan/backlog/help-more-columns.md</c>.</summary>
    private const int WidthMargin = 10;

    /// <summary>The console width assumed for a virtual console or piped/redirected output, where the real width can't be trusted.</summary>
    private const int FallbackConsoleWidth = 80;

    /// <summary>The narrowest a description column is ever wrapped to, even on a very narrow or misreported console width.</summary>
    private const int MinDescriptionWidth = 20;

    /// <summary>Prints the whole <c>--help</c> output: banner, usage line, and every section from <see cref="HelpContent.Sections"/>.</summary>
    public static void Print()
    {
        Console.WriteLine(
            $"whatinator {Core.WhatinatorVersion.Current} - rip CDs, convert to FLAC/MP3, and track metadata via MusicBrainz");
        Console.WriteLine();
        Console.WriteLine("Usage: whatinator <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");

        var descriptionWidth = Math.Max(GetConsoleWidth() - WidthMargin - UsageColumnWidth, MinDescriptionWidth);

        foreach (var section in HelpContent.Sections)
        {
            Console.WriteLine();
            Console.WriteLine($"{section.Title}:");

            foreach (var command in section.Commands)
            {
                foreach (var line in FormatCommand(command, descriptionWidth))
                {
                    Console.WriteLine(line);
                }
            }
        }
    }

    /// <summary>Detects the usable console width, falling back to <see cref="FallbackConsoleWidth"/> for piped output or a virtual console that can't report a real width.</summary>
    /// <returns>The console's current width, in characters.</returns>
    private static int GetConsoleWidth()
    {
        if (Console.IsOutputRedirected)
        {
            return FallbackConsoleWidth;
        }

        try
        {
            var width = Console.WindowWidth;
            return width > 0 ? width : FallbackConsoleWidth;
        }
        catch (IOException)
        {
            return FallbackConsoleWidth;
        }
    }

    /// <summary>Lays out one command's usage lines and word-wrapped body into the final rows to print.</summary>
    /// <param name="command">The command to format.</param>
    /// <param name="descriptionWidth">The width available to word-wrap the right (description) column to.</param>
    /// <returns>The fully formatted lines, left column and right column combined.</returns>
    private static IReadOnlyList<string> FormatCommand(HelpCommand command, int descriptionWidth)
    {
        var optionColumnWidth = command.Body.OfType<HelpOption>()
            .Select(option => option.Flag.Length + 1)
            .DefaultIfEmpty(0)
            .Max();

        var descriptionLines = new List<string>();
        foreach (var block in command.Body)
        {
            descriptionLines.AddRange(block switch
            {
                HelpParagraph paragraph => WrapText(paragraph.Text, descriptionWidth),
                HelpOption option => FormatOption(option, optionColumnWidth, descriptionWidth),
                _ => throw new NotSupportedException($"Unknown {nameof(HelpBlock)} type: {block.GetType()}"),
            });
        }

        var rowCount = Math.Max(command.UsageLines.Count, descriptionLines.Count);
        var rows = new List<string>(rowCount);
        for (var i = 0; i < rowCount; i++)
        {
            var usage = i < command.UsageLines.Count ? command.UsageLines[i] : string.Empty;
            var description = i < descriptionLines.Count ? descriptionLines[i] : string.Empty;
            rows.Add($"  {usage.PadRight(UsageColumnWidth - 2)}{description}".TrimEnd());
        }

        return rows;
    }

    /// <summary>Formats one option as "flag, padded to its command's widest flag, then its word-wrapped description hanging-indented beneath it.</summary>
    /// <param name="option">The option to format.</param>
    /// <param name="columnWidth">The flag column's width -- every option within the same command shares one, so their descriptions all start at the same offset.</param>
    /// <param name="descriptionWidth">The width available to the whole option row (flag column plus description).</param>
    /// <returns>One or more lines, already including the flag column's leading spaces.</returns>
    private static IReadOnlyList<string> FormatOption(HelpOption option, int columnWidth, int descriptionWidth)
    {
        var innerWidth = Math.Max(descriptionWidth - columnWidth, MinDescriptionWidth);
        var wrapped = WrapText(option.Text, innerWidth);
        var lines = new List<string>(wrapped.Count);
        for (var i = 0; i < wrapped.Count; i++)
        {
            var prefix = i == 0 ? option.Flag.PadRight(columnWidth) : new string(' ', columnWidth);
            lines.Add(prefix + wrapped[i]);
        }

        return lines;
    }

    /// <summary>Greedily word-wraps text to the given width, never breaking a word.</summary>
    /// <param name="text">The text to wrap.</param>
    /// <param name="width">The maximum width of each line.</param>
    /// <returns>The wrapped lines -- always at least one, even for empty text.</returns>
    private static IReadOnlyList<string> WrapText(string text, int width)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > width)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        if (current.Length > 0 || lines.Count == 0)
        {
            lines.Add(current.ToString());
        }

        return lines;
    }
}
