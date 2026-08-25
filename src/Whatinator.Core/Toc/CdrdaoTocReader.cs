using System.Diagnostics;
using Whatinator.Core.Rip;

namespace Whatinator.Core.Toc;

/// <summary>
/// Drives <c>cdrdao read-toc</c> as a subprocess and parses its output into
/// a frame-accurate <see cref="DiscToc"/> -- the pregap/index detail
/// <see cref="Whatinator.LibDiscId"/>'s fast TOC-only read deliberately
/// doesn't provide (see root <c>CLAUDE.md</c> § Gotchas: it reads
/// <c>features = 0</c>, MusicBrainz-disc-ID-only).
/// </summary>
public sealed class CdrdaoTocReader : ICdrdaoTocReader
{
    /// <summary>Runs <c>cdrdao read-toc</c> against <paramref name="device"/> and parses the resulting <c>.toc</c> file.</summary>
    /// <param name="device">The block device to read, e.g. <c>/dev/sr1</c>.</param>
    /// <param name="fastToc">
    /// When <see langword="true"/>, passes <c>--fast-toc</c> -- a much
    /// faster read that skips index-point/pregap scanning (track
    /// start/length only). See <see cref="DiscTocTrack.PregapFrames"/> for
    /// exactly what detail this costs.
    /// </param>
    /// <param name="standardOutput">
    /// The stream to relay cdrdao's live progress into (e.g.
    /// <c>Console.OpenStandardError()</c>) -- cdrdao writes all of its own
    /// output to stderr, never stdout, confirmed live; this is that stream,
    /// named to match this project's other subprocess runners.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The parsed, frame-accurate table of contents.</returns>
    /// <exception cref="InvalidOperationException"><c>cdrdao</c> exited with a non-zero code.</exception>
    public async Task<DiscToc> ReadAsync(
        string device,
        bool fastToc,
        Stream standardOutput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(standardOutput);

        // Never pre-created: cdrdao insists on creating this file itself and
        // refuses to write to one that already exists (confirmed live).
        var tocFilePath = Path.Combine(Path.GetTempPath(), $"whatinator-{Guid.NewGuid():N}.toc");

        try
        {
            var filter = new CdrdaoLiveOutputFilter();
            var exitCode = await SubprocessRunner.RunAsync(
                BuildStartInfo(device, fastToc, tocFilePath),
                (reader, ct) => reader.BaseStream.CopyToAsync(Stream.Null, ct),
                (reader, ct) => RelayFilteredAsync(reader, standardOutput, filter, ct),
                cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"cdrdao read-toc exited with code {exitCode}.");
            }

            var tocText = await File.ReadAllTextAsync(tocFilePath, cancellationToken).ConfigureAwait(false);
            var toc = TocFileParser.Parse(tocText, fastToc);

            if (filter.SawCatalogLine)
            {
                // cdrdao's own stderr never prints the value, only "Found disk
                // catalogue number." -- the value only exists in the parsed
                // .toc file's CATALOG line, so it's re-emitted here once
                // parsing is done rather than being available live.
                var line = toc.CatalogNumber is not null
                    ? $"Found disk catalogue number: {toc.CatalogNumber}"
                    : "Found disk catalogue number.";
                await StreamLineWriter.WriteLineAsync(standardOutput, line, cancellationToken).ConfigureAwait(false);
            }

            return toc;
        }
        finally
        {
            if (File.Exists(tocFilePath))
            {
                File.Delete(tocFilePath);
            }
        }
    }

    /// <summary>Builds the <c>cdrdao read-toc</c> process start info.</summary>
    /// <param name="device">The block device to read.</param>
    /// <param name="fastToc">Whether to pass <c>--fast-toc</c>.</param>
    /// <param name="tocFilePath">The (not-yet-existing) path cdrdao should write its <c>.toc</c> file to.</param>
    /// <returns>The configured start info, not yet started.</returns>
    internal static ProcessStartInfo BuildStartInfo(string device, bool fastToc, string tocFilePath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "cdrdao",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add("read-toc");

        if (fastToc)
        {
            startInfo.ArgumentList.Add("--fast-toc");
        }

        startInfo.ArgumentList.Add("--device");
        startInfo.ArgumentList.Add(device);
        startInfo.ArgumentList.Add(tocFilePath);

        return startInfo;
    }

    /// <summary>Relays cdrdao's stderr line by line through <paramref name="filter"/>.</summary>
    private static async Task RelayFilteredAsync(
        StreamReader source,
        Stream destination,
        CdrdaoLiveOutputFilter filter,
        CancellationToken cancellationToken)
    {
        string? line;
        while ((line = await source.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            var output = filter.Process(line);
            if (output is not null)
            {
                await StreamLineWriter.WriteLineAsync(destination, output, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
