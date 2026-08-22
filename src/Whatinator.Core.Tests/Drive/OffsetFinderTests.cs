using Whatinator.Core.AccurateRip;
using Whatinator.Core.Drive;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class OffsetFinderTests
{
    private static readonly DiscToc ThreeTrackToc = new(
    [
        new DiscTocTrack(1, 0, 999, IsAudio: true),
        new DiscTocTrack(2, 1000, 1999, IsAudio: true),
        new DiscTocTrack(3, 2000, 2999, IsAudio: true),
    ]);

    [Fact]
    public async Task FindAsync_ReturnsTooFewTracks_WhenDiscHasFewerThanThreeAudioTracks()
    {
        var toc = new DiscToc([new DiscTocTrack(1, 0, 999, IsAudio: true), new DiscTocTrack(2, 1000, 1999, IsAudio: true)]);
        var entryLookup = new FakeEntryLookup();
        var finder = new OffsetFinder(entryLookup, new FakeTocReader(toc), FailIfCalled);

        var result = await finder.FindAsync("/dev/sr1", new MemoryStream());

        Assert.False(result.Found);
        Assert.Equal(OffsetFindFailureReason.TooFewTracks, result.FailureReason);
        Assert.Equal(0, entryLookup.CallCount);
    }

    [Fact]
    public async Task FindAsync_ReturnsNoAccurateRipEntry_WhenDatabaseHasNoEntries()
    {
        var entryLookup = new FakeEntryLookup([]);
        var finder = new OffsetFinder(entryLookup, new FakeTocReader(ThreeTrackToc), FailIfCalled);

        var result = await finder.FindAsync("/dev/sr1", new MemoryStream());

        Assert.False(result.Found);
        Assert.Equal(OffsetFindFailureReason.NoAccurateRipEntry, result.FailureReason);
    }

    [Fact]
    public async Task FindAsync_ReturnsNoAccurateRipEntry_WhenEveryEntryHasAMismatchedTrackCount()
    {
        // The disc has 3 audio tracks; this entry only covers 2 -- same
        // "malformed/mismatched data, skip it" treatment AccurateRipClient
        // .MatchTrack already gives entries like this.
        var entry = new AccurateRipDbEntry([1, 1], [0x11111111u, 0x22222222u]);
        var entryLookup = new FakeEntryLookup([entry]);
        var finder = new OffsetFinder(entryLookup, new FakeTocReader(ThreeTrackToc), FailIfCalled);

        var result = await finder.FindAsync("/dev/sr1", new MemoryStream());

        Assert.False(result.Found);
        Assert.Equal(OffsetFindFailureReason.NoAccurateRipEntry, result.FailureReason);
    }

    [Fact]
    public async Task FindAsync_FindsTheOffset_WhenEveryTrackButTheLastMatches()
    {
        var correctOffset = OffsetFinder.CandidateOffsets[0];
        byte[] pcm1 = [1, 2, 3, 4];
        byte[] pcm2 = [5, 6, 7, 8];
        var checksum1 = AccurateRipChecksum.Compute(pcm1, 1, 3);
        var checksum2 = AccurateRipChecksum.Compute(pcm2, 2, 3);
        var entry = new AccurateRipDbEntry([1, 1, 1], [checksum1.V1, checksum2.V1, 0u]);
        var entryLookup = new FakeEntryLookup([entry]);
        var readTrackNumbers = new List<int>();

        Task<byte[]?> ReadOnce(string device, DiscToc toc, int trackNumber, int offset, Stream standardOutput, CancellationToken cancellationToken)
        {
            readTrackNumbers.Add(trackNumber);
            if (offset != correctOffset)
            {
                return Task.FromResult<byte[]?>(null);
            }

            return Task.FromResult(trackNumber switch { 1 => pcm1, 2 => pcm2, _ => (byte[]?)null });
        }

        var finder = new OffsetFinder(entryLookup, new FakeTocReader(ThreeTrackToc), ReadOnce);

        var result = await finder.FindAsync("/dev/sr1", new MemoryStream());

        Assert.True(result.Found);
        Assert.Equal(correctOffset, result.Offset);
        Assert.Null(result.FailureReason);

        // Track 3 (the last one) is deliberately never read, even though
        // track 1 matched -- avoids needing overread support just to find
        // an offset.
        Assert.DoesNotContain(3, readTrackNumbers);
    }

    [Fact]
    public async Task FindAsync_TriesCandidateOffsetsInOrder_UntilOneMatches()
    {
        var secondOffset = OffsetFinder.CandidateOffsets[1];
        byte[] pcm1 = [1, 2, 3, 4];
        byte[] pcm2 = [5, 6, 7, 8];
        var checksum1 = AccurateRipChecksum.Compute(pcm1, 1, 3);
        var checksum2 = AccurateRipChecksum.Compute(pcm2, 2, 3);
        var entry = new AccurateRipDbEntry([1, 1, 1], [checksum1.V1, checksum2.V1, 0u]);
        var entryLookup = new FakeEntryLookup([entry]);
        var triedOffsets = new List<int>();

        Task<byte[]?> ReadOnce(string device, DiscToc toc, int trackNumber, int offset, Stream standardOutput, CancellationToken cancellationToken)
        {
            if (trackNumber == 1)
            {
                triedOffsets.Add(offset);
            }

            if (offset != secondOffset)
            {
                return Task.FromResult<byte[]?>(null);
            }

            return Task.FromResult(trackNumber switch { 1 => pcm1, 2 => pcm2, _ => (byte[]?)null });
        }

        var finder = new OffsetFinder(entryLookup, new FakeTocReader(ThreeTrackToc), ReadOnce);

        var result = await finder.FindAsync("/dev/sr1", new MemoryStream());

        Assert.Equal(secondOffset, result.Offset);
        Assert.Equal([OffsetFinder.CandidateOffsets[0], secondOffset], triedOffsets);
    }

    [Fact]
    public async Task FindAsync_ReturnsNoOffsetMatched_WhenATrackNeverMatchesAtAnyOffset()
    {
        byte[] pcm1 = [1, 2, 3, 4];
        var checksum1 = AccurateRipChecksum.Compute(pcm1, 1, 3);

        // Track 1 "matches" at every offset (unrealistic for a real drive,
        // but isolates the case where track 1 always passes and some other
        // track never does) -- the search must still exhaust every
        // candidate rather than accepting a partial match.
        var entry = new AccurateRipDbEntry([1, 1, 1], [checksum1.V1, 0xDEADBEEFu, 0u]);
        var entryLookup = new FakeEntryLookup([entry]);

        Task<byte[]?> ReadOnce(string device, DiscToc toc, int trackNumber, int offset, Stream standardOutput, CancellationToken cancellationToken) =>
            Task.FromResult(trackNumber switch
            {
                1 => pcm1,
                2 => (byte[]?)[9, 9, 9, 9],
                _ => null,
            });

        var finder = new OffsetFinder(entryLookup, new FakeTocReader(ThreeTrackToc), ReadOnce);

        var result = await finder.FindAsync("/dev/sr1", new MemoryStream());

        Assert.False(result.Found);
        Assert.Equal(OffsetFindFailureReason.NoOffsetMatched, result.FailureReason);
    }

    private static Task<byte[]?> FailIfCalled(string device, DiscToc toc, int trackNumber, int offset, Stream standardOutput, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A track read should not have been attempted.");

    private sealed class FakeTocReader : ICdrdaoTocReader
    {
        private readonly DiscToc _toc;

        public FakeTocReader(DiscToc toc)
        {
            _toc = toc;
        }

        public Task<DiscToc> ReadAsync(string device, bool fastToc, Stream standardOutput, CancellationToken cancellationToken = default) =>
            Task.FromResult(_toc);
    }

    private sealed class FakeEntryLookup : IAccurateRipEntryLookup
    {
        private readonly IReadOnlyList<AccurateRipDbEntry> _entries;

        public FakeEntryLookup(IReadOnlyList<AccurateRipDbEntry>? entries = null)
        {
            _entries = entries ?? [];
        }

        public int CallCount { get; private set; }

        public Task<IReadOnlyList<AccurateRipDbEntry>> GetEntriesAsync(DiscToc toc, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_entries);
        }
    }
}
