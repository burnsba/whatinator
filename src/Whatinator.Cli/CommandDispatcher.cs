namespace Whatinator.Cli;

/// <summary>Parses top-level command-line arguments and dispatches to the matching command.</summary>
internal static class CommandDispatcher
{
    /// <summary>Runs the CLI with the given arguments.</summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <param name="httpClientFactory">
    /// The shared factory commands resolve their <see cref="HttpClient"/>s
    /// from (only <c>disc-info</c>/<c>make-releaseinfo</c>/<c>rip</c>/
    /// <c>flac</c>/<c>pipeline</c>/<c>offset-find</c> need one -- everything
    /// else has no network dependency).
    /// </param>
    /// <param name="cancellationToken">
    /// Cancelled when the user hits Ctrl-C (see <c>Program.cs</c>). Threaded
    /// into every command whose underlying <c>Whatinator.Core</c> calls
    /// accept one.
    /// </param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        if (args.Length == 0)
        {
            HelpFormatter.Print();
            return 0;
        }

        var command = args[0];
        var rest = args[1..];

        switch (command)
        {
            case "--help":
            case "-h":
            case "help":
                HelpFormatter.Print();
                return 0;
            case "--version":
            case "-v":
                Console.WriteLine($"whatinator {Core.WhatinatorVersion.Current}");
                return 0;
            case "list-device":
                return ListDeviceCommand.Run(rest);
            case "disc-info":
                return await DiscInfoCommand.RunAsync(rest, httpClientFactory, cancellationToken).ConfigureAwait(false);
            case "toc":
                return await TocCommand.RunAsync(rest, cancellationToken).ConfigureAwait(false);
            case "make-releaseinfo":
                return await MakeReleaseInfoCommand.RunAsync(rest, httpClientFactory, cancellationToken).ConfigureAwait(false);
            case "id-txt":
                return IdTxtCommand.Run(rest);
            case "rip":
                return await RipCommand.RunAsync(rest, httpClientFactory, cancellationToken).ConfigureAwait(false);
            case "flac":
                return await FlacCommand.RunAsync(rest, httpClientFactory, cancellationToken).ConfigureAwait(false);
            case "mp3":
                return await Mp3Command.RunAsync(rest, cancellationToken).ConfigureAwait(false);
            case "pipeline":
                return await PipelineCommand.RunAsync(rest, httpClientFactory, cancellationToken).ConfigureAwait(false);
            case "offset-find":
                return await OffsetFindCommand.RunAsync(rest, httpClientFactory, cancellationToken).ConfigureAwait(false);
            case "cache-check":
                return await CacheCheckCommand.RunAsync(rest, cancellationToken).ConfigureAwait(false);
            case "make-checksum":
                return MakeChecksumCommand.Run(rest);
            case "compare-checksum":
                return CompareChecksumCommand.Run(rest);
            case "update-metadata":
                return UpdateMetadataCommand.Run(rest);
            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                Console.Error.WriteLine();
                HelpFormatter.Print(Console.Error);
                return 1;
        }
    }
}
