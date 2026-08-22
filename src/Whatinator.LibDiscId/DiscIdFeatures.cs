namespace Whatinator.LibDiscId;

/// <summary>
/// Feature flags accepted by <see cref="DiscReader.Read"/>, mirroring
/// libdiscid's <c>discid_feature_t</c>.
/// </summary>
/// <remarks>
/// This project deliberately defaults to and mostly only uses
/// <see cref="None"/>: reading the TOC alone takes about a second, while
/// <see cref="Mcn"/> and <see cref="Isrc"/> both require a slow
/// Q-subchannel pass through the entire disc. See root <c>CLAUDE.md</c> §
/// Gotchas for why that distinction matters for this project.
/// </remarks>
[Flags]
public enum DiscIdFeatures : uint
{
    /// <summary>TOC-only read. The disc ID is always available with no extra flags.</summary>
    None = 0,

    /// <summary>Read the Media Catalogue Number. Requires a slow subchannel pass.</summary>
    Mcn = 1 << 1,

    /// <summary>Read per-track ISRCs. Requires a slow subchannel pass.</summary>
    Isrc = 1 << 2,
}
