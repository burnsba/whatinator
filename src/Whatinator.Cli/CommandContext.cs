using Whatinator.Core;
using Whatinator.Core.Drive;

namespace Whatinator.Cli;

/// <summary>
/// Config and device resolution shared by every command: reads the config
/// file once per invocation and applies the standard <c>--device</c>/<c>-d</c>
/// override, rather than each command re-deriving it independently (some
/// inline inside a <c>??</c>, some via a held local -- see the CLI
/// duplication backlog item).
/// </summary>
/// <param name="Config">The loaded config.</param>
/// <param name="Device">The device path to use: <c>--device</c>/<c>-d</c> if given, else <see cref="WhatinatorConfig.Device"/>.</param>
internal sealed record CommandContext(WhatinatorConfig Config, string Device)
{
    /// <summary>Loads the config and resolves the device for one command invocation.</summary>
    /// <param name="options">
    /// The caller's already-parsed options. Must have been parsed with
    /// <c>OptionSpec.Value("--device", "-d")</c> among its specs.
    /// </param>
    /// <returns>The resolved context.</returns>
    public static CommandContext Resolve(ParsedOptions options)
    {
        var config = ConfigLoader.Load();
        var device = options.GetValue("--device") ?? config.Device;
        return new CommandContext(config, device);
    }

    /// <summary>Looks up the optical drive at <see cref="Device"/>, if any is currently enumerable.</summary>
    /// <returns>The matching drive, or <see langword="null"/> if none enumerates at that device path.</returns>
    public OpticalDrive? ResolveDrive() =>
        OpticalDriveLocator.Enumerate().FirstOrDefault(d => d.DevicePath == Device);
}
