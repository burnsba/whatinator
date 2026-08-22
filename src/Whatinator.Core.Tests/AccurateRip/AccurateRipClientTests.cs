using System.Buffers.Binary;
using System.Net;
using Whatinator.Core.AccurateRip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class AccurateRipClientTests
{
    private static readonly DiscToc TwoTrackToc = new(
    [
        new DiscTocTrack(1, 0, 999, IsAudio: true),
        new DiscTocTrack(2, 1000, 1999, IsAudio: true),
    ]);

    [Fact]
    public async Task MatchAsync_ReturnsNotFound_On404()
    {
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var result = await client.MatchAsync(TwoTrackToc, [(1u, 2u), (3u, 4u)]);

        Assert.False(result.Found);
        Assert.Equal(2, result.Tracks.Count);
        Assert.All(result.Tracks, t => Assert.False(t.IsMatch));
        Assert.Equal(1u, result.Tracks[0].ComputedV1);
        Assert.Null(result.Tracks[0].MaxConfidence);
    }

    [Fact]
    public async Task MatchAsync_ReturnsNotFound_OnNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var client = CreateClient(handler);

        var result = await client.MatchAsync(TwoTrackToc, [(1u, 2u), (3u, 4u)]);

        Assert.False(result.Found);
    }

    [Fact]
    public async Task MatchAsync_RequestsTheAccurateRipPathBuiltFromDiscIds()
    {
        string? requestedPath = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestedPath = request.RequestUri!.AbsolutePath;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var client = CreateClient(handler);

        await client.MatchAsync(TwoTrackToc, [(1u, 2u), (3u, 4u)]);

        var (discId1, discId2) = AccurateRipDiscId.Compute(TwoTrackToc);
        var cddbId = CddbDiscId.Compute(TwoTrackToc);
        Assert.Equal(
            $"/accuraterip/{discId1[^1]}/{discId1[^2]}/{discId1[^3]}/dBAR-002-{discId1}-{discId2}-{cddbId}.bin",
            requestedPath);
    }

    [Fact]
    public async Task MatchAsync_MatchesComputedChecksumAgainstResponseEntry()
    {
        // Track 1's computed v1 (0x11111111) matches the entry's CRC; v2
        // (0x22222222) does not match anything in this entry.
        var entry = BuildEntry(
            trackCount: 2,
            discId1: 0,
            discId2: 0,
            cddbId: 0,
            (Confidence: 5, Crc: 0x11111111),
            (Confidence: 3, Crc: 0xaaaaaaaa));
        var client = CreateClient(BinaryResponseHandler(entry));

        var result = await client.MatchAsync(TwoTrackToc, [(0x11111111u, 0x22222222u), (0x99999999u, 0x99999999u)]);

        Assert.True(result.Found);
        var track1 = result.Tracks[0];
        Assert.True(track1.IsMatch);
        Assert.Equal("11111111", track1.MatchedCrcV1);
        Assert.Equal((byte)5, track1.ConfidenceV1);
        Assert.Null(track1.MatchedCrcV2);
        Assert.Equal((byte)5, track1.MaxConfidence);

        var track2 = result.Tracks[1];
        Assert.False(track2.IsMatch);
        Assert.Equal((byte)3, track2.MaxConfidence);
        Assert.Equal("aaaaaaaa", track2.MaxConfidenceCrc);
    }

    [Fact]
    public async Task MatchAsync_MultipleEntries_KeepsHighestConfidenceMatch()
    {
        var lowConfidenceEntry = BuildEntry(2, 0, 0, 0, (2, 0x11111111), (1, 0x22222222));
        var highConfidenceEntry = BuildEntry(2, 0, 0, 0, (9, 0x11111111), (1, 0x33333333));
        var response = Concat(lowConfidenceEntry, highConfidenceEntry);
        var client = CreateClient(BinaryResponseHandler(response));

        var result = await client.MatchAsync(TwoTrackToc, [(0x11111111u, 0u), (0u, 0u)]);

        Assert.Equal((byte)9, result.Tracks[0].ConfidenceV1);
        Assert.Equal("11111111", result.Tracks[0].MatchedCrcV1);
    }

    [Fact]
    public async Task MatchAsync_MismatchedComputedChecksumCount_Throws()
    {
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.MatchAsync(TwoTrackToc, [(1u, 2u)]));
    }

    [Fact]
    public async Task MatchAsync_RealAccurateRipFixture_MatchesGenuineDatabaseEntry()
    {
        // Captured live during phase 012's hands-on demo against a real
        // disc in /dev/sr1: an actual 11-track CD's real response from the
        // AccurateRip database (Fixtures/dBAR-011-...bin). The reconstructed
        // TOC below is built from that same disc's real libdiscid track
        // offsets (converted from libdiscid's MSF-based numbering to the
        // LBA-based convention DiscToc/CddbDiscId expect -- see root
        // CLAUDE.md § Gotchas) and its computed disc IDs are asserted to
        // match the fixture's own filename encoding as a sanity check.
        // Track 1's checksum was computed from a real cd-paranoia rip of
        // that same disc, shifted by the drive's actual +6-sample read
        // offset (found by brute force during the demo -- automating that
        // discovery is phase 017's job, not yet built). At that shift, both
        // v1 and v2 exactly matched two separate confidence-200 entries in
        // the real database response below.
        var toc = RealElevenTrackDiscToc();
        var (discId1, discId2) = AccurateRipDiscId.Compute(toc);
        Assert.Equal("00127f7c", discId1);
        Assert.Equal("00a2b21c", discId2);
        Assert.Equal("8e0b360b", CddbDiscId.Compute(toc));

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "AccurateRip", "Fixtures", "dBAR-011-00127f7c-00a2b21c-8e0b360b.bin");
        var response = await File.ReadAllBytesAsync(fixturePath);
        var client = CreateClient(BinaryResponseHandler(response));

        var computedChecksums = new List<(uint V1, uint V2)> { (0x8cff983du, 0x98445115u) };
        computedChecksums.AddRange(Enumerable.Repeat((0u, 0u), toc.Tracks.Count - 1));

        var result = await client.MatchAsync(toc, computedChecksums);

        Assert.True(result.Found);
        var track1 = result.Tracks[0];
        Assert.True(track1.IsMatch);
        Assert.Equal("8cff983d", track1.MatchedCrcV1);
        Assert.Equal((byte)200, track1.ConfidenceV1);
        Assert.Equal("98445115", track1.MatchedCrcV2);
        Assert.Equal((byte)200, track1.ConfidenceV2);
    }

    [Fact]
    public async Task GetEntriesAsync_ReturnsEmpty_On404()
    {
        var client = CreateClient(new StubHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var entries = await client.GetEntriesAsync(TwoTrackToc);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetEntriesAsync_ReturnsEmpty_OnNetworkFailure()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var client = CreateClient(handler);

        var entries = await client.GetEntriesAsync(TwoTrackToc);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetEntriesAsync_ParsesEveryEntryFromTheBinaryResponse()
    {
        var first = BuildEntry(2, 0, 0, 0, (5, 0x11111111), (3, 0xaaaaaaaa));
        var second = BuildEntry(2, 0, 0, 0, (9, 0x22222222), (1, 0xbbbbbbbb));
        var client = CreateClient(BinaryResponseHandler(Concat(first, second)));

        var entries = await client.GetEntriesAsync(TwoTrackToc);

        Assert.Equal(2, entries.Count);
        Assert.Equal(0x11111111u, entries[0].Checksums[0]);
        Assert.Equal((byte)5, entries[0].Confidences[0]);
        Assert.Equal(0x22222222u, entries[1].Checksums[0]);
        Assert.Equal((byte)9, entries[1].Confidences[0]);
    }

    /// <summary>
    /// The real 11-track TOC backing <c>Fixtures/dBAR-011-...bin</c>, built
    /// from libdiscid's actual per-track offsets/lengths (MSF-based,
    /// converted to LBA) read from a real disc in <c>/dev/sr1</c>.
    /// </summary>
    private static DiscToc RealElevenTrackDiscToc()
    {
        (int OffsetSectors, int LengthSectors)[] tracks =
        [
            (182, 16715), (16897, 17105), (34002, 17718), (51720, 18247),
            (69967, 17538), (87505, 22367), (109872, 11238), (121110, 15632),
            (136742, 37908), (174650, 21355), (196005, 19427),
        ];

        const int msfToLbaDelta = 150;
        var discTocTracks = new List<DiscTocTrack>();
        for (var i = 0; i < tracks.Length; i++)
        {
            var start = tracks[i].OffsetSectors - msfToLbaDelta;
            var end = start + tracks[i].LengthSectors - 1;
            discTocTracks.Add(new DiscTocTrack(i + 1, start, end, IsAudio: true));
        }

        return new DiscToc(discTocTracks);
    }

    private static AccurateRipClient CreateClient(HttpMessageHandler handler) =>
        new("whatinator-tests/1.0 ( test@example.com )", new HttpClient(handler));

    private static StubHttpMessageHandler BinaryResponseHandler(byte[] content) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });

    /// <summary>Builds one AccurateRip response entry in the database's exact wire format.</summary>
    private static byte[] BuildEntry(byte trackCount, uint discId1, uint discId2, uint cddbId, params (byte Confidence, uint Crc)[] tracks)
    {
        var bytes = new byte[1 + 12 + (trackCount * 9)];
        bytes[0] = trackCount;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(1, 4), discId1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(5, 4), discId2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(9, 4), cddbId);

        var pos = 13;
        foreach (var (confidence, crc) in tracks)
        {
            bytes[pos] = confidence;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(pos + 1, 4), crc);
            pos += 9;
        }

        return bytes;
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(p => p)];
}
