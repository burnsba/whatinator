using System.Buffers.Binary;
using Whatinator.Core.Toc;

namespace Whatinator.Core.AccurateRip;

/// <summary>A thin client for the AccurateRip database.</summary>
/// <remarks>
/// Best-effort by design, same contract as this project's MusicBrainz/
/// Discogs/Cover-Art clients: a 404 or network failure just means "not in
/// the database" -- never an exception. Confirmed live: the database is a
/// flat, unauthenticated HTTP GET (plain <c>http</c>, not <c>https</c> --
/// confirmed, not a typo) returning a binary, possibly multi-entry response.
/// Implements both <see cref="IAccurateRipClient"/> (the whole-disc match
/// used by <see cref="Rip.WhatinatorRipRunner"/>) and
/// <see cref="IAccurateRipEntryLookup"/> (the raw per-entry data used by
/// <see cref="Drive.OffsetFinder"/>) -- one HTTP fetch/parse path
/// (<see cref="FetchEntriesAsync"/>) backs both.
/// </remarks>
public sealed class AccurateRipClient : IAccurateRipClient, IAccurateRipEntryLookup
{
    /// <summary>The base URL for the AccurateRip database.</summary>
    public const string BaseUrl = "http://www.accuraterip.com/accuraterip/";

    private readonly HttpClient _httpClient;

    /// <summary>Initializes a new instance of the <see cref="AccurateRipClient"/> class.</summary>
    /// <param name="httpClient">
    /// The <see cref="HttpClient"/> to issue requests with -- owned and
    /// configured by the caller (typically resolved from a shared
    /// <c>IHttpClientFactory</c>), not disposed by this class. Configuration
    /// -- <see cref="System.Net.Http.HttpClient.BaseAddress"/> (see
    /// <see cref="BaseUrl"/>) and the <c>User-Agent</c> header -- is entirely
    /// the caller's responsibility; this constructor does not touch either.
    /// Tests pass
    /// <c>new HttpClient(stubHandler) { BaseAddress = new Uri(BaseUrl) }</c>
    /// instead of hitting the real network.
    /// </param>
    public AccurateRipClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<AccurateRipMatchResult> MatchAsync(
        DiscToc toc,
        IReadOnlyList<(uint V1, uint V2)> computedChecksums,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toc);
        ArgumentNullException.ThrowIfNull(computedChecksums);

        var audioTracks = toc.Tracks.Where(t => t.IsAudio).ToList();
        if (audioTracks.Count != computedChecksums.Count)
        {
            throw new ArgumentException(
                $"Expected {audioTracks.Count} computed checksums (one per audio track), got {computedChecksums.Count}.",
                nameof(computedChecksums));
        }

        var entries = await FetchEntriesAsync(toc, cancellationToken).ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return NotFoundResult(audioTracks, computedChecksums);
        }

        var tracks = new List<AccurateRipTrackMatch>(audioTracks.Count);
        for (var i = 0; i < audioTracks.Count; i++)
        {
            tracks.Add(MatchTrack(audioTracks[i].TrackNumber, computedChecksums[i], i, audioTracks.Count, entries));
        }

        return new AccurateRipMatchResult { Found = true, Tracks = tracks };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AccurateRipDbEntry>> GetEntriesAsync(DiscToc toc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toc);

        return await FetchEntriesAsync(toc, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Splits a raw AccurateRip response into its (possibly multiple)
    /// fixed-format entries -- ported from prior research into this exact
    /// binary format (see root <c>CLAUDE.md</c> § Gotchas). A response entry that claims
    /// more bytes than remain in the buffer is dropped rather than throwing
    /// -- this is untrusted external data. Each entry's 12-byte header
    /// carries the response's own disc IDs; an entry whose header doesn't
    /// match <paramref name="expectedDiscId1"/>/<paramref name="expectedDiscId2"/>/
    /// <paramref name="expectedCddbId"/> is dropped too -- a server-side
    /// mismatch, a stale caching proxy, or a truncated/misaligned read could
    /// otherwise be matched against this disc's checksums as though it were
    /// genuinely ours (see root <c>CLAUDE.md</c> § Gotchas).
    /// </summary>
    /// <param name="raw">The raw response body.</param>
    /// <param name="expectedDiscId1">The disc's own computed first AccurateRip disc ID, as returned by <see cref="AccurateRipDiscId.Compute"/>.</param>
    /// <param name="expectedDiscId2">The disc's own computed second AccurateRip disc ID, as returned by <see cref="AccurateRipDiscId.Compute"/>.</param>
    /// <param name="expectedCddbId">The disc's own computed CDDB disc ID, as returned by <see cref="CddbDiscId.Compute"/>.</param>
    /// <returns>Every entry whose header matches the expected disc IDs.</returns>
    internal static List<AccurateRipDbEntry> ParseEntries(byte[] raw, string expectedDiscId1, string expectedDiscId2, string expectedCddbId)
    {
        var entries = new List<AccurateRipDbEntry>();
        var offset = 0;
        while (offset < raw.Length)
        {
            var trackCount = raw[offset];
            var entryLength = 1 + 12 + (trackCount * 9);
            if (offset + entryLength > raw.Length)
            {
                break;
            }

            var headerDiscId1 = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset + 1, 4)).ToString("x8");
            var headerDiscId2 = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset + 5, 4)).ToString("x8");
            var headerCddbId = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(offset + 9, 4)).ToString("x8");
            if (headerDiscId1 != expectedDiscId1 || headerDiscId2 != expectedDiscId2 || headerCddbId != expectedCddbId)
            {
                offset += entryLength;
                continue;
            }

            var confidences = new byte[trackCount];
            var checksums = new uint[trackCount];
            var pos = offset + 13;
            for (var t = 0; t < trackCount; t++)
            {
                confidences[t] = raw[pos];
                checksums[t] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(pos + 1, 4));

                // The remaining 4 bytes of this 9-byte record (pos+5..pos+8,
                // skipped by the pos += 9 below) are the offset-finding CRC:
                // a checksum of samples around frame 450 of the track, used
                // by some clients to detect a drive's pressing offset from a
                // partial read instead of a full-track read. It carries no
                // v1/v2 identity for the primary CRC above -- see MatchTrack.
                // Not consumed here: this client verifies whole tracks, and
                // Drive.OffsetFinder already determines offset by comparing
                // full-track checksums (via IAccurateRipEntryLookup /
                // AccurateRipChecksum), not a frame-level probe.
                pos += 9;
            }

            entries.Add(new AccurateRipDbEntry(confidences, checksums));
            offset += entryLength;
        }

        return entries;
    }

    /// <summary>Fetches and parses a disc's raw AccurateRip database entries -- the shared body of <see cref="MatchAsync"/> and <see cref="GetEntriesAsync"/>.</summary>
    /// <param name="toc">The disc's table of contents.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>Every parsed entry, or an empty list on a 404/network failure/empty response.</returns>
    private async Task<List<AccurateRipDbEntry>> FetchEntriesAsync(DiscToc toc, CancellationToken cancellationToken)
    {
        var (discId1, discId2) = AccurateRipDiscId.Compute(toc);
        var cddbId = CddbDiscId.Compute(toc);

        try
        {
            using var response = await _httpClient.GetAsync(BuildPath(discId1, discId2, cddbId, toc), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return ParseEntries(body, discId1, discId2, cddbId);
        }
        catch (HttpRequestException)
        {
            return [];
        }
    }

    /// <summary>Matches one track's computed checksums against every database entry's value at its position.</summary>
    /// <param name="trackNumber">The track's 1-based disc position.</param>
    /// <param name="computed">The track's locally computed v1/v2 checksums.</param>
    /// <param name="index">The track's 0-based position among audio tracks -- how database entries index their per-track arrays.</param>
    /// <param name="audioTrackCount">The disc's total audio track count, used to skip malformed/mismatched entries.</param>
    /// <param name="entries">Every entry parsed from the database response.</param>
    private static AccurateRipTrackMatch MatchTrack(
        int trackNumber,
        (uint V1, uint V2) computed,
        int index,
        int audioTrackCount,
        List<AccurateRipDbEntry> entries)
    {
        byte? maxConfidence = null;
        string? maxConfidenceCrc = null;
        byte? confidenceV1 = null;
        string? crcV1 = null;
        byte? confidenceV2 = null;
        string? crcV2 = null;

        foreach (var entry in entries)
        {
            if (entry.Checksums.Length != audioTrackCount)
            {
                continue;
            }

            var confidence = entry.Confidences[index];
            var crc = entry.Checksums[index];
            var crcHex = crc.ToString("x8");

            if (maxConfidence is null || confidence > maxConfidence)
            {
                maxConfidence = confidence;
                maxConfidenceCrc = crcHex;
            }

            // Each entry's single CRC field carries no marker for which
            // AccurateRip algorithm (v1 or v2) produced it -- the dBAR
            // format simply doesn't have one. So it's tested against both
            // locally computed values, and equality with one of them *is*
            // the version identification: a served v1 CRC equalling
            // computed.V2 by coincidence needs a ~2^-32 collision. The one
            // case that's genuinely ambiguous is computed.V1 == computed.V2
            // for this track (same coincidence odds) -- WhatinatorEacLog's
            // FormatAccurateRipLine detects that and omits the version
            // label rather than asserting one.
            if (crc == computed.V1 && (confidenceV1 is null || confidence > confidenceV1))
            {
                confidenceV1 = confidence;
                crcV1 = crcHex;
            }

            if (crc == computed.V2 && (confidenceV2 is null || confidence > confidenceV2))
            {
                confidenceV2 = confidence;
                crcV2 = crcHex;
            }
        }

        return new AccurateRipTrackMatch
        {
            TrackNumber = trackNumber,
            ComputedV1 = computed.V1,
            ComputedV2 = computed.V2,
            MatchedCrcV1 = crcV1,
            ConfidenceV1 = confidenceV1,
            MatchedCrcV2 = crcV2,
            ConfidenceV2 = confidenceV2,
            MaxConfidence = maxConfidence,
            MaxConfidenceCrc = maxConfidenceCrc,
        };
    }

    /// <summary>Builds a "not found" result -- every track carries its computed checksums but no database data.</summary>
    /// <param name="audioTracks">The disc's audio tracks.</param>
    /// <param name="computedChecksums">One (v1, v2) checksum pair per audio track, in track order.</param>
    private static AccurateRipMatchResult NotFoundResult(
        IReadOnlyList<DiscTocTrack> audioTracks,
        IReadOnlyList<(uint V1, uint V2)> computedChecksums)
    {
        var tracks = new List<AccurateRipTrackMatch>(audioTracks.Count);
        for (var i = 0; i < audioTracks.Count; i++)
        {
            tracks.Add(new AccurateRipTrackMatch
            {
                TrackNumber = audioTracks[i].TrackNumber,
                ComputedV1 = computedChecksums[i].V1,
                ComputedV2 = computedChecksums[i].V2,
            });
        }

        return new AccurateRipMatchResult { Found = false, Tracks = tracks };
    }

    /// <summary>
    /// Builds an AccurateRip lookup path from a disc's computed disc IDs --
    /// ported from prior research into this exact URL scheme (see root
    /// <c>CLAUDE.md</c> § Gotchas).
    /// </summary>
    /// <param name="discId1">The disc's first computed AccurateRip disc ID, as returned by <see cref="AccurateRipDiscId.Compute"/>.</param>
    /// <param name="discId2">The disc's second computed AccurateRip disc ID, as returned by <see cref="AccurateRipDiscId.Compute"/>.</param>
    /// <param name="cddbId">The disc's computed CDDB disc ID, as returned by <see cref="CddbDiscId.Compute"/>.</param>
    /// <param name="toc">The disc's table of contents.</param>
    private static string BuildPath(string discId1, string discId2, string cddbId, DiscToc toc)
    {
        var audioTrackCount = toc.Tracks.Count(t => t.IsAudio);
        return $"{discId1[^1]}/{discId1[^2]}/{discId1[^3]}/dBAR-{audioTrackCount:D3}-{discId1}-{discId2}-{cddbId}.bin";
    }
}
