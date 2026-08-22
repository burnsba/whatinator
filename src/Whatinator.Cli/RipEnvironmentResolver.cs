using Whatinator.Core;
using Whatinator.Core.Drive;
using Whatinator.Core.Rip;

namespace Whatinator.Cli;

/// <summary>
/// Gathers a <see cref="RipEnvironmentInfo"/> for the EAC-style rip log
/// (phase 016) -- shared by <c>rip</c> and <c>pipeline</c>, the two commands
/// that actually drive <see cref="WhatinatorRipRunner"/>.
/// </summary>
internal static class RipEnvironmentResolver
{
    /// <summary>Resolves drive/tool environment info for <paramref name="drive"/>.</summary>
    /// <param name="config">The loaded config, for <see cref="WhatinatorConfig.GetCacheDefeat"/>.</param>
    /// <param name="drive">The resolved drive, or <see langword="null"/> if it couldn't be matched against <see cref="OpticalDriveLocator.Enumerate"/>.</param>
    /// <returns>The resolved environment info.</returns>
    public static RipEnvironmentInfo Resolve(WhatinatorConfig config, OpticalDrive? drive) =>
        new(
            drive?.Vendor,
            drive?.Model,
            drive?.Release,
            config.GetCacheDefeat(drive?.Vendor, drive?.Model, drive?.Release),
            SystemInfo.GetCdParanoiaVersion(),
            SystemInfo.GetCdrdaoVersion(),
            SystemInfo.GetFlacVersion(),
            SystemInfo.GetUname(),
            SystemInfo.GetOsPrettyName());
}
