namespace Whatinator.Core.Rip;

/// <summary>
/// The <c>yyyyMMdd-HHmmss: </c> line prefix used for console output once a
/// rip is underway -- see root <c>CLAUDE.md</c> § Gotchas for exactly which
/// output does and doesn't get this prefix (MusicBrainz/Discogs selection
/// and the TOC/ISRC "startup" section deliberately don't; everything from
/// the "starting: ..." announcement through each track's read does).
/// </summary>
public static class RipOutputTimestamp
{
    /// <summary>Formats the current local time as a line prefix, e.g. <c>20260820-071103: </c>.</summary>
    /// <returns>The formatted prefix, including the trailing <c>": "</c>.</returns>
    public static string Prefix() => $"{DateTime.Now:yyyyMMdd-HHmmss}: ";
}
