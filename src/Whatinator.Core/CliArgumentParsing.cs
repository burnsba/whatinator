using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Whatinator.Core.Metadata;

namespace Whatinator.Core;

/// <summary>
/// Pure argument-validation and releaseinfo-loading helpers shared by
/// <c>Whatinator.Cli</c>'s command implementations. Lives here rather than in
/// <c>Whatinator.Cli</c> for the same reason as <see cref="CliExceptionBoundary"/>:
/// that project has no test project by design (see root <c>CLAUDE.md</c>) --
/// this is the testable seam the CLI commands delegate to.
/// </summary>
public static class CliArgumentParsing
{
    /// <summary>
    /// Parses an optional <c>--disc</c> argument value into a disc number.
    /// </summary>
    /// <param name="discArg">The raw <c>--disc</c> value, or <see langword="null"/> if the option was not given.</param>
    /// <param name="error">A one-line error message if parsing failed, otherwise <see langword="null"/>.</param>
    /// <param name="discNumber">The parsed disc number, or <see langword="null"/> if <paramref name="discArg"/> was <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="discArg"/> is absent or a valid integer.</returns>
    public static bool TryParseDiscNumber(string? discArg, [NotNullWhen(false)] out string? error, out int? discNumber)
    {
        if (discArg is null)
        {
            discNumber = null;
            error = null;
            return true;
        }

        if (!int.TryParse(discArg, out var parsed))
        {
            discNumber = null;
            error = $"--disc must be a number, got '{discArg}'.";
            return false;
        }

        discNumber = parsed;
        error = null;
        return true;
    }

    /// <summary>
    /// Loads a <c>releaseinfo.json</c> file, translating I/O and JSON
    /// failures into a one-line error message instead of letting them
    /// propagate as a stack trace.
    /// </summary>
    /// <param name="path">The path to load.</param>
    /// <param name="releaseInfo">The loaded release, or <see langword="null"/> on failure.</param>
    /// <param name="error">A one-line error message on failure, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the file was loaded successfully.</returns>
    public static bool TryLoadReleaseInfo(string path, [NotNullWhen(true)] out ReleaseInfo? releaseInfo, [NotNullWhen(false)] out string? error)
    {
        try
        {
            releaseInfo = ReleaseInfoFile.Load(path);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            releaseInfo = null;
            error = $"Failed to read {path}: {ex.Message}";
            return false;
        }
    }
}
