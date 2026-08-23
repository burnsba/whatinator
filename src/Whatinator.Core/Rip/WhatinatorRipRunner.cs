using Whatinator.Core.AccurateRip;
using Whatinator.Core.Flac;
using Whatinator.Core.Naming;

namespace Whatinator.Core.Rip;

/// <summary>
/// Rips a whole disc via native <c>cdrdao</c>/<c>cd-paranoia</c>/<c>flac</c>
/// subprocesses, composing phases 012-014: for every audio track, <see cref="CdParanoiaTrackReader"/>'s
/// test/copy read-and-verify cycle, an <see cref="AccurateRipChecksum"/>
/// computation, then a <see cref="FlacEncoder"/> encode -- followed by one
/// whole-disc <see cref="IAccurateRipClient"/> lookup once every track has
/// been read. Data tracks are skipped with a warning (no data-track ripping,
/// matching this project's existing scope). Deliberately doesn't call
/// <see cref="FlacPackager"/> itself -- same separation of concerns
/// <see cref="PipelineRunner"/> already uses to compose its packagers.
/// </summary>
public sealed class WhatinatorRipRunner
{
    private readonly IAccurateRipClient _accurateRipClient;
    private readonly ICdParanoiaTrackReader _trackReader;
    private readonly IFlacEncoder _flacEncoder;

    /// <summary>Initializes a new instance of the <see cref="WhatinatorRipRunner"/> class.</summary>
    /// <param name="accurateRipClient">The AccurateRip database client to use for the whole-disc lookup.</param>
    public WhatinatorRipRunner(IAccurateRipClient accurateRipClient)
        : this(accurateRipClient, new CdParanoiaTrackReader(), new FlacEncoder())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WhatinatorRipRunner"/>
    /// class with fake <paramref name="trackReader"/>/<paramref name="flacEncoder"/>
    /// implementations -- a test seam so this class's orchestration/
    /// sequencing logic (track loop, <c>KeepWav</c> handling, AccurateRip
    /// merge) can be exercised without spawning real <c>cd-paranoia</c>/
    /// <c>flac</c> processes, same intent as this project's other internal-
    /// constructor test seams (e.g. <c>MusicBrainzClient</c>'s retry delay).
    /// </summary>
    /// <param name="accurateRipClient">The AccurateRip database client to use for the whole-disc lookup.</param>
    /// <param name="trackReader">The track reader to use.</param>
    /// <param name="flacEncoder">The FLAC encoder to use.</param>
    internal WhatinatorRipRunner(IAccurateRipClient accurateRipClient, ICdParanoiaTrackReader trackReader, IFlacEncoder flacEncoder)
    {
        ArgumentNullException.ThrowIfNull(accurateRipClient);
        ArgumentNullException.ThrowIfNull(trackReader);
        ArgumentNullException.ThrowIfNull(flacEncoder);
        _accurateRipClient = accurateRipClient;
        _trackReader = trackReader;
        _flacEncoder = flacEncoder;
    }

    /// <summary>Rips every audio track on one disc, encoding each to FLAC.</summary>
    /// <param name="options">The rip options, including an already-read <see cref="Whatinator.Core.Toc.DiscToc"/>.</param>
    /// <param name="standardOutput">The stream to relay <c>flac</c>'s live progress and this runner's own <c>Track N of M</c> announcements into.</param>
    /// <param name="standardError">The stream to relay <c>cd-paranoia</c>'s/<c>flac</c>'s stderr and this runner's own warnings into.</param>
    /// <param name="cancellationToken">A token to cancel the rip.</param>
    /// <returns>Every track's outcome and the whole-disc AccurateRip match, if attempted.</returns>
    /// <exception cref="ArgumentException"><see cref="WhatinatorRipOptions.DiscNumber"/> is missing or out of range for a multi-disc release.</exception>
    /// <exception cref="InvalidOperationException">
    /// The disc's TOC has an audio track with no matching entry in
    /// <see cref="WhatinatorRipOptions.ReleaseInfo"/>'s metadata, or
    /// <c>flac</c> fails on a track.
    /// </exception>
    public async Task<WhatinatorRipResult> RipAsync(
        WhatinatorRipOptions options,
        Stream standardOutput,
        Stream standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        Directory.CreateDirectory(options.OutputDirectory);

        var discNumber = ReleaseFolderNaming.ResolveDiscNumber(options.ReleaseInfo, options.DiscNumber);
        var medium = options.ReleaseInfo.Media.Single(m => m.Position == discNumber);

        var audioTracks = options.Toc.Tracks.Where(t => t.IsAudio).ToList();
        var dataTrackCount = options.Toc.Tracks.Count - audioTracks.Count;
        if (dataTrackCount > 0)
        {
            await StreamLineWriter.WriteLineAsync(
                standardError,
                $"Warning: skipping {dataTrackCount} data track(s) on this disc -- not ripped.",
                cancellationToken,
                timestamped: true).ConfigureAwait(false);
        }

        var year = ReleaseFolderNaming.ExtractYear(options.ReleaseInfo.Date);
        var yearOrNull = year == "0000" ? null : year;

        var results = new List<WhatinatorTrackRipResult>(audioTracks.Count);
        var checksums = new List<(uint V1, uint V2)>(audioTracks.Count);

        for (var i = 0; i < audioTracks.Count; i++)
        {
            var tocTrack = audioTracks[i];
            var track = medium.Tracks.SingleOrDefault(t => t.Number == tocTrack.TrackNumber)
                ?? throw new InvalidOperationException(
                    $"Disc {discNumber}'s TOC has an audio track {tocTrack.TrackNumber} with no matching entry in the release metadata.");

            // Neither cd-paranoia nor flac has a concept of "this is track 3
            // of an 11-track batch" -- each is invoked once per track, so this
            // orchestrator is the only thing that knows the batch shape.
            await StreamLineWriter.WriteLineAsync(standardOutput, $"Track {i + 1} of {audioTracks.Count}: {track.Title}", cancellationToken, timestamped: true)
                .ConfigureAwait(false);

            var baseFileName = TrackFileNaming.BuildBaseFileName(options.ReleaseInfo, track);
            var wavPath = Path.Combine(options.OutputDirectory, baseFileName + ".wav");

            var readResult = await _trackReader.ReadTrackAsync(
                new CdParanoiaTrackOptions(options.Device, options.Toc, tocTrack.TrackNumber, wavPath, options.Offset, options.Overread, options.MaxRetries),
                standardError,
                cancellationToken).ConfigureAwait(false);

            if (readResult.Degraded)
            {
                results.Add(new WhatinatorTrackRipResult(tocTrack.TrackNumber, true, null, null, null, null, null, readResult.Attempts));
                continue;
            }

            // Position among audio tracks only (1-based) -- the AccurateRip
            // checksum algorithm indexes by ripped-track position, not
            // physical TOC track number; a data track ahead of the
            // first audio track must not shift which track gets the
            // first-track 5-sector trim. AccurateRipClient.MatchAsync
            // indexes database entries the same way.
            checksums.Add(AccurateRipChecksum.Compute(WavFile.ReadDataChunk(readResult.WavPath!), i + 1, audioTracks.Count));

            await StreamLineWriter.WriteLineAsync(standardOutput, "Converting WAV to FLAC...", cancellationToken, timestamped: true).ConfigureAwait(false);

            var flacPath = Path.Combine(options.OutputDirectory, baseFileName + ".flac");
            var encodeOptions = new FlacEncodeOptions(
                readResult.WavPath!,
                flacPath,
                track.Title,
                track.Artist,
                options.ReleaseInfo.Title,
                options.ReleaseInfo.Artist,
                yearOrNull,
                track.Number,
                medium.Tracks.Count,
                options.ReleaseInfo.Discogs?.Genre,
                tocTrack.Isrc);
            var encodeResult = await _flacEncoder.EncodeAsync(encodeOptions, standardOutput, standardError, cancellationToken)
                .ConfigureAwait(false);
            if (!encodeResult.Success)
            {
                throw new InvalidOperationException($"flac exited with code {encodeResult.ExitCode} encoding '{readResult.WavPath}'.");
            }

            string? keptWavPath = null;
            if (options.KeepWav)
            {
                keptWavPath = readResult.WavPath;
            }
            else
            {
                File.Delete(readResult.WavPath!);
            }

            results.Add(new WhatinatorTrackRipResult(
                tocTrack.TrackNumber, false, flacPath, keptWavPath, readResult.Crc32, readResult.Peak, readResult.Quality, readResult.Attempts, ElapsedTime: readResult.ElapsedTime));
        }

        if (results.Any(r => r.Degraded))
        {
            // The whole-disc AccurateRip lookup needs a checksum for every
            // audio track (AccurateRipClient.MatchAsync itself enforces this
            // count) -- a skipped track means it can't be attempted at all,
            // not just partially. Never fatal either way.
            return new WhatinatorRipResult(results, false, dataTrackCount);
        }

        var arResult = await _accurateRipClient.MatchAsync(options.Toc, checksums, cancellationToken).ConfigureAwait(false);

        var merged = new List<WhatinatorTrackRipResult>(results.Count);
        for (var i = 0; i < results.Count; i++)
        {
            merged.Add(results[i] with { AccurateRip = arResult.Tracks[i] });
        }

        return new WhatinatorRipResult(merged, arResult.Found, dataTrackCount);
    }
}
