using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Whatinator.Core.Metadata;

namespace Whatinator.Core;

/// <summary>
/// Pure argument-validation and releaseinfo-loading helpers shared by
/// <c>Whatinator.Cli</c>'s command implementations. Lives here rather than in
/// <c>Whatinator.Cli</c> for the same reason as <see cref="CliExceptionBoundary"/>:
/// it needs <see cref="Metadata.ReleaseInfoFile"/> and nothing from the
/// console/process layer, so it belongs with the rest of Core's testable
/// logic rather than the thin CLI shell (see root <c>CLAUDE.md</c>).
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
    /// Resolves <c>--retries</c>/<c>--max-sector-reads</c>/<c>--stall-timeout</c>
    /// for <c>rip</c>/<c>pipeline</c>: an explicit CLI value wins, falling back
    /// to <paramref name="config"/>'s value, falling back to a hardcoded
    /// default (5 / 12 / 1200). Also enforces that
    /// <paramref name="maxSectorReadsArg"/>/<paramref name="stallTimeoutArg"/>
    /// cannot be given alongside <paramref name="noVerify"/> -- tuning the
    /// robustness knobs while also opting out of local verification is a
    /// contradictory request the CLI rejects outright rather than silently
    /// ignoring one side (checked against the raw argument strings, not the
    /// resolved values, so only an explicitly-given flag trips it -- a config
    /// default applying underneath <c>--no-verify</c> is fine and intentional,
    /// since both remain real safety nets against a single sector or a
    /// stalled read hanging the whole run). See root <c>CLAUDE.md</c> and
    /// <c>docs/backlog-completed/050-eac-gap-extraction-mode-and-retry-control.md</c>
    /// for how these three settings compose multiplicatively into one
    /// track's worst-case wall-clock time.
    /// </summary>
    /// <param name="noVerify">Whether <c>--no-verify</c> was given.</param>
    /// <param name="retriesArg">The raw <c>--retries</c> value, or <see langword="null"/> if not given.</param>
    /// <param name="maxSectorReadsArg">The raw <c>--max-sector-reads</c> value, or <see langword="null"/> if not given.</param>
    /// <param name="stallTimeoutArg">The raw <c>--stall-timeout</c> value, or <see langword="null"/> if not given.</param>
    /// <param name="config">The loaded config, supplying <see cref="WhatinatorConfig.MaxSectorReads"/>/<see cref="WhatinatorConfig.StallTimeoutSeconds"/> fallbacks.</param>
    /// <param name="error">A one-line error message if resolution failed, otherwise <see langword="null"/>.</param>
    /// <param name="maxRetries">The resolved <see cref="Rip.CdParanoiaTrackOptions.MaxRetries"/> value.</param>
    /// <param name="maxSectorReads">The resolved <see cref="Rip.CdParanoiaTrackOptions.MaxSectorReads"/> value.</param>
    /// <param name="stallTimeoutSeconds">The resolved <see cref="Rip.CdParanoiaTrackOptions.StallTimeoutSeconds"/> value.</param>
    /// <returns><see langword="true"/> if every given value parsed as a non-negative integer and the <c>--no-verify</c> conflict rule wasn't violated.</returns>
    public static bool TryResolveRetryOptions(
        bool noVerify,
        string? retriesArg,
        string? maxSectorReadsArg,
        string? stallTimeoutArg,
        WhatinatorConfig config,
        [NotNullWhen(false)] out string? error,
        out int maxRetries,
        out int maxSectorReads,
        out int stallTimeoutSeconds)
    {
        ArgumentNullException.ThrowIfNull(config);

        maxRetries = 0;
        maxSectorReads = 0;
        stallTimeoutSeconds = 0;

        if (noVerify && maxSectorReadsArg is not null)
        {
            error = "--max-sector-reads cannot be combined with --no-verify.";
            return false;
        }

        if (noVerify && stallTimeoutArg is not null)
        {
            error = "--stall-timeout cannot be combined with --no-verify.";
            return false;
        }

        if (!TryParseNonNegativeInt(retriesArg, "--retries", out maxRetries, out error))
        {
            return false;
        }

        if (retriesArg is null)
        {
            maxRetries = 5;
        }

        if (!TryParseNonNegativeInt(maxSectorReadsArg, "--max-sector-reads", out maxSectorReads, out error))
        {
            return false;
        }

        if (maxSectorReadsArg is null)
        {
            maxSectorReads = config.MaxSectorReads ?? 12;
        }

        if (!TryParseNonNegativeInt(stallTimeoutArg, "--stall-timeout", out stallTimeoutSeconds, out error))
        {
            return false;
        }

        if (stallTimeoutArg is null)
        {
            stallTimeoutSeconds = config.StallTimeoutSeconds ?? 1200;
        }

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

    /// <summary>Parses an optional non-negative integer argument, e.g. one of <see cref="TryResolveRetryOptions"/>'s value options.</summary>
    /// <param name="arg">The raw value, or <see langword="null"/> if not given.</param>
    /// <param name="optionName">The option's long name, for the error message.</param>
    /// <param name="value">The parsed value, or <c>0</c> if <paramref name="arg"/> is <see langword="null"/> or invalid.</param>
    /// <param name="error">A one-line error message if <paramref name="arg"/> is non-null but not a valid non-negative integer, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="arg"/> is absent or a valid non-negative integer.</returns>
    private static bool TryParseNonNegativeInt(string? arg, string optionName, out int value, [NotNullWhen(false)] out string? error)
    {
        if (arg is null)
        {
            value = 0;
            error = null;
            return true;
        }

        if (!int.TryParse(arg, out var parsed) || parsed < 0)
        {
            value = 0;
            error = $"{optionName} must be a non-negative number, got '{arg}'.";
            return false;
        }

        value = parsed;
        error = null;
        return true;
    }
}
