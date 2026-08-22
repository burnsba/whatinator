using Whatinator.Core.Drive;

namespace Whatinator.Core.Rip;

/// <summary>
/// Drive/tool environment data for a rip, gathered by the caller
/// (<c>RipCommand</c>/<c>PipelineCommand</c> -- sysfs reads, config lookups,
/// and <c>--version</c> subprocess calls all belong at the CLI wiring layer,
/// same as how <see cref="WhatinatorConfig.GetReadOffset"/>'s result is
/// already resolved by the caller rather than by
/// <see cref="WhatinatorRipRunner"/> itself). Feeds <see cref="EacLogOptions"/>
/// when a caller wants a rip log written -- phase 016.
/// </summary>
/// <param name="DriveVendor">The drive's vendor string, or <see langword="null"/> if unknown.</param>
/// <param name="DriveModel">The drive's model string, or <see langword="null"/> if unknown.</param>
/// <param name="DriveRelease">The drive's firmware revision, or <see langword="null"/> if unknown.</param>
/// <param name="CacheDefeat">
/// The drive's known <see cref="CacheDefeatResult"/>, from
/// <see cref="WhatinatorConfig.GetCacheDefeat"/> -- never analyzed live
/// per-rip (see that method's remarks on why).
/// </param>
/// <param name="CdParanoiaVersion">The <c>cd-paranoia --version</c> banner, from <see cref="SystemInfo.GetCdParanoiaVersion"/>.</param>
/// <param name="CdrdaoVersion">The <c>cdrdao</c> version banner, from <see cref="SystemInfo.GetCdrdaoVersion"/>.</param>
/// <param name="FlacVersion">The <c>flac --version</c> output, from <see cref="SystemInfo.GetFlacVersion"/>.</param>
/// <param name="Uname">The <c>uname -a</c> output, from <see cref="SystemInfo.GetUname"/>.</param>
/// <param name="OsPrettyName">The OS's <c>PRETTY_NAME</c>, from <see cref="SystemInfo.GetOsPrettyName"/>.</param>
public sealed record RipEnvironmentInfo(
    string? DriveVendor,
    string? DriveModel,
    string? DriveRelease,
    CacheDefeatResult CacheDefeat,
    string CdParanoiaVersion,
    string CdrdaoVersion,
    string FlacVersion,
    string Uname,
    string? OsPrettyName);
