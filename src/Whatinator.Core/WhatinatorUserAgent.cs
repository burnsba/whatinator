namespace Whatinator.Core;

/// <summary>
/// Builds whatinator's default HTTP <c>User-Agent</c> header, sent with every
/// MusicBrainz, Discogs, Cover Art Archive, and AccurateRip request. Single
/// source for what was previously six identical, independently-stale
/// <c>private const string</c> copies across <c>Whatinator.Cli</c>'s command
/// files -- see <see cref="WhatinatorConfig.EffectiveUserAgent"/> for the
/// override path.
/// </summary>
public static class WhatinatorUserAgent
{
    /// <summary>
    /// The contact address baked into <see cref="Default"/>. MusicBrainz
    /// treats <c>User-Agent</c> as its rate-limiting/abuse-triage key, so a
    /// working contact address matters -- see <c>MusicBrainzClient</c>'s
    /// constructor doc.
    /// </summary>
    public const string DefaultContactEmail = "bethany.whatinator@burnsba.net";

    /// <summary>
    /// The default <c>User-Agent</c> string: <c>whatinator/{version} ( {email} )</c>,
    /// with the version read fresh from <see cref="WhatinatorVersion.Current"/>
    /// on every access so it can never drift from the assembly's actual
    /// version, unlike the literal it replaces.
    /// </summary>
    public static string Default => $"whatinator/{WhatinatorVersion.Current} ( {DefaultContactEmail} )";
}
