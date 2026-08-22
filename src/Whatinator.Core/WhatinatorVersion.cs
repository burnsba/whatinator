using System.Reflection;

namespace Whatinator.Core;

/// <summary>
/// whatinator's own release version. The single source of truth is
/// <c>&lt;Version&gt;</c> in the root <c>Directory.Build.props</c> -- read
/// here via assembly metadata so nothing else has to duplicate the literal.
/// </summary>
public static class WhatinatorVersion
{
    /// <summary>The current whatinator version, e.g. <c>1.0.0</c>.</summary>
    public static string Current { get; } =
        typeof(WhatinatorVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
