using System.Text.Json;
using System.Text.Json.Serialization;

namespace Whatinator.Core;

/// <summary>Loads the whatinator user config file, falling back to built-in defaults if absent.</summary>
public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true,
    };

    /// <summary>
    /// Loads config from <paramref name="configPath"/> (or the platform
    /// default -- see <see cref="ResolveDefaultPath"/> -- if not given).
    /// Returns built-in <see cref="WhatinatorConfig"/> defaults if the file
    /// doesn't exist; this is the normal, expected case for a user who
    /// hasn't created one.
    /// </summary>
    /// <param name="configPath">
    /// The config file to load. Defaults to <see cref="ResolveDefaultPath"/>;
    /// overridable for testing.
    /// </param>
    /// <returns>The loaded configuration, or defaults if no file exists.</returns>
    /// <exception cref="JsonException">The config file exists but isn't valid JSON.</exception>
    public static WhatinatorConfig Load(string? configPath = null)
    {
        var path = configPath ?? ResolveDefaultPath();
        if (!File.Exists(path))
        {
            return new WhatinatorConfig();
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<WhatinatorConfig>(stream, JsonOptions) ?? new WhatinatorConfig();
    }

    /// <summary>
    /// Writes <paramref name="config"/> to <paramref name="configPath"/> (or
    /// the platform default), creating its parent directory if needed and
    /// overwriting the file if it already exists. Phase 017's
    /// <c>offset-find</c> command is this project's first writer -- every
    /// prior per-drive config map (<see cref="WhatinatorConfig.ReadOffsets"/>/
    /// <see cref="WhatinatorConfig.CacheDefeats"/>) was hand-edited until now.
    /// </summary>
    /// <param name="config">The configuration to persist.</param>
    /// <param name="configPath">
    /// The file to write. Defaults to <see cref="ResolveDefaultPath"/>;
    /// overridable for testing.
    /// </param>
    public static void Save(WhatinatorConfig config, string? configPath = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var path = configPath ?? ResolveDefaultPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
    }

    /// <summary>
    /// Resolves the platform-default config file path:
    /// <c>$XDG_CONFIG_HOME/whatinator/config.json</c>, falling back to
    /// <c>~/.config/whatinator/config.json</c> if that variable isn't set.
    /// </summary>
    /// <returns>The default config file path. Does not check whether it exists.</returns>
    public static string ResolveDefaultPath()
    {
        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configHome = string.IsNullOrWhiteSpace(xdgConfigHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdgConfigHome;

        return Path.Combine(configHome, "whatinator", "config.json");
    }
}
