using Whatinator.Core.AccurateRip;
using Whatinator.Core.CoverArt;
using Whatinator.Core.Flac;
using Whatinator.Core.Mp3;
using Whatinator.Core.Naming;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Rip;

/// <summary>
/// Composes <see cref="Toc.CdrdaoTocReader"/>, <see cref="WhatinatorRipRunner"/>,
/// <see cref="FlacPackager"/>, and <see cref="Mp3Packager"/> into the full
/// TOC-read → rip → FLAC-packaging → MP3 pipeline for a single disc, per the
/// phase 008 end-to-end command. Ripping every disc of a multi-disc release
/// (prompting between physical disc swaps) is the caller's job -- this class
/// only knows about one disc per call, same "one thing per class" shape as
/// the packagers it wraps.
/// </summary>
public sealed class PipelineRunner
{
    private readonly CdrdaoTocReader _tocReader = new();
    private readonly WhatinatorRipRunner _ripRunner;
    private readonly FlacPackager _flacPackager;
    private readonly Mp3Packager _mp3Packager = new();

    /// <summary>Initializes a new instance of the <see cref="PipelineRunner"/> class.</summary>
    /// <param name="coverArtClient">The cover art client <see cref="FlacPackager"/> uses.</param>
    /// <param name="accurateRipClient">The AccurateRip database client <see cref="WhatinatorRipRunner"/> uses.</param>
    public PipelineRunner(ICoverArtClient coverArtClient, IAccurateRipClient accurateRipClient)
    {
        ArgumentNullException.ThrowIfNull(coverArtClient);
        ArgumentNullException.ThrowIfNull(accurateRipClient);
        _flacPackager = new FlacPackager(coverArtClient);
        _ripRunner = new WhatinatorRipRunner(accurateRipClient);
    }

    /// <summary>Rips, then (unless skipped) packages, one disc.</summary>
    /// <param name="options">The disc's pipeline options.</param>
    /// <param name="standardOutput">The stream to relay live rip/encode progress into.</param>
    /// <param name="standardError">The stream to relay subprocess stderr into.</param>
    /// <param name="cancellationToken">A token to cancel the run.</param>
    /// <returns>What happened for this disc.</returns>
    /// <exception cref="ArgumentException"><see cref="PipelineDiscOptions.DiscNumber"/> is missing or out of range for a multi-disc release.</exception>
    public async Task<PipelineDiscResult> RunDiscAsync(
        PipelineDiscOptions options,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        // Validated up front so a bad --disc value fails before spinning up the
        // drive, not after a real (possibly lengthy) rip completes.
        var discNumber = ReleaseFolderNaming.ResolveDiscNumber(options.ReleaseInfo, options.DiscNumber);
        var isMultiDisc = options.ReleaseInfo.Media.Count > 1;

        var rawDir = Path.Combine(
            options.DestinationParentDirectory,
            isMultiDisc ? $"whatinator-raw-rip-cd{discNumber}" : "whatinator-raw-rip");
        if (Directory.Exists(rawDir))
        {
            // Leftover from a previous crashed/aborted run -- start from a
            // clean slate rather than erroring out.
            Directory.Delete(rawDir, recursive: true);
        }

        var toc = await _tocReader.ReadAsync(options.Device, fastToc: true, standardError, cancellationToken).ConfigureAwait(false);

        var ripOptions = new WhatinatorRipOptions(
            options.Device,
            options.ReleaseInfo,
            toc,
            rawDir,
            DiscNumber: isMultiDisc ? discNumber : null,
            Offset: options.Offset ?? 0,
            Overread: options.Overread,
            KeepWav: options.KeepWav);
        var startTime = DateTimeOffset.UtcNow;
        var ripResult = await _ripRunner.RipAsync(ripOptions, standardOutput, standardError, cancellationToken)
            .ConfigureAwait(false);
        var endTime = DateTimeOffset.UtcNow;

        if (!ripResult.Success && !ripResult.Degraded)
        {
            return new PipelineDiscResult(rawDir, ripResult, null, null);
        }

        // Written into rawDir now, before FlacPackager relocates anything --
        // FlacPackager's existing "move a .log file if present" mechanism
        // (unchanged since phase 006) picks it up from there like any other
        // rip output file. The log's own Filename lines reference where
        // FlacPackager will actually leave the files (computed the same way
        // FlacPackager itself does), not the scratch rawDir they're written
        // to first -- rawDir gets deleted by this same method a few lines
        // down, so a Filename line pointing there would already be stale by
        // the time anyone reads the finished log. Falls back to rawDir only
        // when SkipFlacPackaging leaves the files there permanently.
        if (options.Environment is not null)
        {
            var containerDir = Path.Combine(
                options.DestinationParentDirectory,
                FlacFolderNaming.ContainerFolderName(options.ReleaseInfo));
            var eventualDiscDirectory = options.SkipFlacPackaging
                ? rawDir
                : isMultiDisc ? Path.Combine(containerDir, ReleaseFolderNaming.DiscFolderName(discNumber)) : containerDir;
            var logOptions = new EacLogOptions(
                options.ReleaseInfo,
                ripResult,
                toc,
                eventualDiscDirectory,
                options.Device,
                options.Environment.DriveVendor,
                options.Environment.DriveModel,
                options.Environment.DriveRelease,
                ripOptions.Offset,
                options.Overread,
                options.Environment.CacheDefeat,
                options.Environment.CdParanoiaVersion,
                options.Environment.CdrdaoVersion,
                options.Environment.FlacVersion,
                options.Environment.Uname,
                options.Environment.OsPrettyName,
                startTime,
                endTime);
            var releaseDisplayName = ReleaseFolderNaming.ReleaseDisplayName(options.ReleaseInfo);
            WhatinatorEacLog.Write(logOptions, Path.Combine(rawDir, releaseDisplayName + ".log"));
        }

        FlacPackageResult? flacResult = null;
        if (!options.SkipFlacPackaging)
        {
            flacResult = await _flacPackager
                .PackageAsync(new FlacPackageOptions(options.ReleaseInfo, rawDir, options.DestinationParentDirectory, discNumber, toc.CatalogNumber))
                .ConfigureAwait(false);
        }

        Mp3PackageResult? mp3Result = null;
        if (options.CreateMp3)
        {
            var mp3Source = flacResult?.DiscDirectory ?? rawDir;
            mp3Result = await _mp3Packager
                .PackageAsync(
                    new Mp3PackageOptions(options.ReleaseInfo, mp3Source, options.DestinationParentDirectory, discNumber),
                    standardOutput,
                    standardError,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!options.SkipFlacPackaging)
        {
            // FlacPackager already moved everything useful out via File.Move;
            // whatever's left is scratch, not output.
            Directory.Delete(rawDir, recursive: true);
        }

        return new PipelineDiscResult(rawDir, ripResult, flacResult, mp3Result);
    }
}
