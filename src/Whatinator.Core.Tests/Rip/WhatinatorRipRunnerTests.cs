using System.Text;
using Whatinator.Core.AccurateRip;
using Whatinator.Core.Flac;
using Whatinator.Core.Metadata;
using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class WhatinatorRipRunnerTests : IDisposable
{
    private static readonly DiscToc SingleAudioTrackToc = new([new DiscTocTrack(1, 0, 999, IsAudio: true)]);

    private readonly string _tempDir;

    public WhatinatorRipRunnerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-whatinatorrip-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task RipAsync_EncodesEveryAudioTrack_AndSkipsDataTracks()
    {
        var toc = new DiscToc(
        [
            new DiscTocTrack(1, 0, 999, IsAudio: false),
            new DiscTocTrack(2, 1000, 1999, IsAudio: true),
            new DiscTocTrack(3, 2000, 2999, IsAudio: true),
        ]);
        var releaseInfo = CreateReleaseInfo([2, 3]);
        var reader = new FakeCdParanoiaTrackReader();
        var encoder = new FakeFlacEncoder();
        var runner = new WhatinatorRipRunner(new FakeAccurateRipClient(), reader, encoder);

        var result = await RunAsync(runner, releaseInfo, toc);

        Assert.Equal(1, result.SkippedDataTrackCount);
        Assert.Equal(2, result.Tracks.Count);
        Assert.Equal([2, 3], reader.ReadTrackNumbers);
        Assert.Equal(2, encoder.EncodedInputPaths.Count);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task RipAsync_PassesTocTrackIsrc_ToFlacEncodeOptions()
    {
        var toc = new DiscToc(
        [
            new DiscTocTrack(1, 0, 999, IsAudio: true, Isrc: "USRC17607839"),
            new DiscTocTrack(2, 1000, 1999, IsAudio: true),
        ]);
        var releaseInfo = CreateReleaseInfo([1, 2]);
        var encoder = new FakeFlacEncoder();
        var runner = new WhatinatorRipRunner(new FakeAccurateRipClient(), new FakeCdParanoiaTrackReader(), encoder);

        await RunAsync(runner, releaseInfo, toc);

        Assert.Equal("USRC17607839", encoder.EncodedOptions[0].Isrc);
        Assert.Null(encoder.EncodedOptions[1].Isrc);
    }

    [Fact]
    public async Task RipAsync_AnnouncesFlacConversion_BeforeEncodingEachTrack()
    {
        var toc = SingleAudioTrackToc;
        var releaseInfo = CreateReleaseInfo([1]);
        var runner = new WhatinatorRipRunner(new FakeAccurateRipClient(), new FakeCdParanoiaTrackReader(), new FakeFlacEncoder());
        var options = new WhatinatorRipOptions("/dev/sr1", releaseInfo, toc, _tempDir);
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        await runner.RipAsync(options, stdout, stderr);

        var output = Encoding.UTF8.GetString(stdout.ToArray());
        Assert.Contains("Converting WAV to FLAC...", output);
    }

    [Fact]
    public async Task RipAsync_DeletesWav_WhenKeepWavFalse()
    {
        var toc = SingleAudioTrackToc;
        var releaseInfo = CreateReleaseInfo([1]);
        var reader = new FakeCdParanoiaTrackReader();
        var runner = new WhatinatorRipRunner(new FakeAccurateRipClient(), reader, new FakeFlacEncoder());

        var result = await RunAsync(runner, releaseInfo, toc, keepWav: false);

        Assert.Null(result.Tracks[0].WavFilePath);
        Assert.False(File.Exists(reader.WavPaths[0]));
    }

    [Fact]
    public async Task RipAsync_KeepsWav_WhenKeepWavTrue()
    {
        var toc = SingleAudioTrackToc;
        var releaseInfo = CreateReleaseInfo([1]);
        var reader = new FakeCdParanoiaTrackReader();
        var runner = new WhatinatorRipRunner(new FakeAccurateRipClient(), reader, new FakeFlacEncoder());

        var result = await RunAsync(runner, releaseInfo, toc, keepWav: true);

        Assert.Equal(reader.WavPaths[0], result.Tracks[0].WavFilePath);
        Assert.True(File.Exists(reader.WavPaths[0]));
    }

    [Fact]
    public async Task RipAsync_MarksTrackDegraded_WhenReaderExhaustsRetries()
    {
        var toc = new DiscToc([new DiscTocTrack(1, 0, 999, IsAudio: true), new DiscTocTrack(2, 1000, 1999, IsAudio: true)]);
        var releaseInfo = CreateReleaseInfo([1, 2]);
        var reader = new FakeCdParanoiaTrackReader(degradedTracks: [1]);
        var runner = new WhatinatorRipRunner(new FakeAccurateRipClient(), reader, new FakeFlacEncoder());

        var result = await RunAsync(runner, releaseInfo, toc);

        Assert.True(result.Degraded);
        Assert.False(result.Success);
        Assert.True(result.Tracks.Single(t => t.TrackNumber == 1).Degraded);
        Assert.False(result.Tracks.Single(t => t.TrackNumber == 2).Degraded);
    }

    [Fact]
    public async Task RipAsync_SkipsAccurateRipLookup_WhenAnyTrackDegraded()
    {
        var toc = new DiscToc([new DiscTocTrack(1, 0, 999, IsAudio: true), new DiscTocTrack(2, 1000, 1999, IsAudio: true)]);
        var releaseInfo = CreateReleaseInfo([1, 2]);
        var reader = new FakeCdParanoiaTrackReader(degradedTracks: [1]);
        var accurateRipClient = new FakeAccurateRipClient();
        var runner = new WhatinatorRipRunner(accurateRipClient, reader, new FakeFlacEncoder());

        var result = await RunAsync(runner, releaseInfo, toc);

        Assert.False(result.AccurateRipFound);
        Assert.Equal(0, accurateRipClient.CallCount);
        Assert.All(result.Tracks, t => Assert.Null(t.AccurateRip));
    }

    [Fact]
    public async Task RipAsync_MergesAccurateRipMatchesByTrackPosition()
    {
        var toc = new DiscToc([new DiscTocTrack(1, 0, 999, IsAudio: true), new DiscTocTrack(2, 1000, 1999, IsAudio: true)]);
        var releaseInfo = CreateReleaseInfo([1, 2]);
        var accurateRipClient = new FakeAccurateRipClient(checksums => new AccurateRipMatchResult
        {
            Found = true,
            Tracks =
            [
                new AccurateRipTrackMatch { TrackNumber = 1, ComputedV1 = checksums[0].V1, ComputedV2 = checksums[0].V2, MatchedCrcV1 = "aaaaaaaa", ConfidenceV1 = 5 },
                new AccurateRipTrackMatch { TrackNumber = 2, ComputedV1 = checksums[1].V1, ComputedV2 = checksums[1].V2, MatchedCrcV2 = "bbbbbbbb", ConfidenceV2 = 9 },
            ],
        });
        var runner = new WhatinatorRipRunner(accurateRipClient, new FakeCdParanoiaTrackReader(), new FakeFlacEncoder());

        var result = await RunAsync(runner, releaseInfo, toc);

        Assert.True(result.AccurateRipFound);
        Assert.Equal(1, accurateRipClient.CallCount);
        Assert.Equal("aaaaaaaa", result.Tracks[0].AccurateRip?.MatchedCrcV1);
        Assert.Equal("bbbbbbbb", result.Tracks[1].AccurateRip?.MatchedCrcV2);
    }

    [Fact]
    public async Task RipAsync_ThrowsArgumentException_WhenDiscNumberMissingForMultiDiscRelease()
    {
        var toc = SingleAudioTrackToc;
        var releaseInfo = CreateMultiDiscRelease();
        var runner = new WhatinatorRipRunner(new FakeAccurateRipClient(), new FakeCdParanoiaTrackReader(), new FakeFlacEncoder());
        var options = new WhatinatorRipOptions("/dev/sr1", releaseInfo, toc, _tempDir);
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(() => runner.RipAsync(options, stdout, stderr));
    }

    [Fact]
    public async Task RipAsync_ThrowsInvalidOperationException_WhenTocTrackHasNoMatchingReleaseTrack()
    {
        var toc = SingleAudioTrackToc;
        var releaseInfo = CreateReleaseInfo([2]); // TOC's only audio track is #1, not #2.
        var runner = new WhatinatorRipRunner(new FakeAccurateRipClient(), new FakeCdParanoiaTrackReader(), new FakeFlacEncoder());

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(runner, releaseInfo, toc));
    }

    [Fact]
    public async Task RipAsync_ThrowsInvalidOperationException_WhenFlacEncodeFails()
    {
        var toc = SingleAudioTrackToc;
        var releaseInfo = CreateReleaseInfo([1]);
        var runner = new WhatinatorRipRunner(new FakeAccurateRipClient(), new FakeCdParanoiaTrackReader(), new FakeFlacEncoder(exitCode: 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(runner, releaseInfo, toc));
    }

    private async Task<WhatinatorRipResult> RunAsync(
        WhatinatorRipRunner runner,
        ReleaseInfo releaseInfo,
        DiscToc toc,
        int? discNumber = null,
        bool keepWav = false)
    {
        var options = new WhatinatorRipOptions("/dev/sr1", releaseInfo, toc, _tempDir, DiscNumber: discNumber, KeepWav: keepWav);
        using var stdout = new MemoryStream();
        using var stderr = new MemoryStream();
        return await runner.RipAsync(options, stdout, stderr);
    }

    private static ReleaseInfo CreateReleaseInfo(IReadOnlyList<int> trackNumbers) => new(
        MusicBrainzReleaseId: "release-id",
        MusicBrainzUrl: "https://musicbrainz.org/release/release-id",
        Artist: "Artist",
        Title: "Album",
        Date: "2000-01-01",
        Country: "US",
        Barcode: null,
        Label: null,
        CatalogNumber: null,
        Media: [new MediumInfo(1, null, trackNumbers.Select(n => new TrackInfo(n, $"Track {n}", "Artist", TimeSpan.FromSeconds(100 + n))).ToList())]);

    private static ReleaseInfo CreateMultiDiscRelease()
    {
        List<MediumInfo> media =
        [
            new MediumInfo(1, null, [new TrackInfo(1, "D1 Track One", "Artist", TimeSpan.FromSeconds(100))]),
            new MediumInfo(2, null, [new TrackInfo(1, "D2 Track One", "Artist", TimeSpan.FromSeconds(100))]),
        ];

        return new ReleaseInfo(
            MusicBrainzReleaseId: "release-id",
            MusicBrainzUrl: "https://musicbrainz.org/release/release-id",
            Artist: "Artist",
            Title: "Album",
            Date: "2000-01-01",
            Country: "US",
            Barcode: null,
            Label: null,
            CatalogNumber: null,
            Media: media);
    }

    private sealed class FakeCdParanoiaTrackReader : ICdParanoiaTrackReader
    {
        private readonly HashSet<int> _degradedTracks;

        public FakeCdParanoiaTrackReader(IEnumerable<int>? degradedTracks = null)
        {
            _degradedTracks = degradedTracks?.ToHashSet() ?? [];
        }

        public List<int> ReadTrackNumbers { get; } = [];

        public List<string> WavPaths { get; } = [];

        public Task<CdParanoiaTrackResult> ReadTrackAsync(
            CdParanoiaTrackOptions options,
            Stream standardOutput,
            CancellationToken cancellationToken = default)
        {
            ReadTrackNumbers.Add(options.TrackNumber);
            WavPaths.Add(options.DestinationWavPath);

            if (_degradedTracks.Contains(options.TrackNumber))
            {
                return Task.FromResult(new CdParanoiaTrackResult(false, null, null, null, null, 5));
            }

            WriteSyntheticWav(options.DestinationWavPath);
            return Task.FromResult(new CdParanoiaTrackResult(true, options.DestinationWavPath, 0xDEADBEEF, 1000, 1.0, 1));
        }

        private static void WriteSyntheticWav(string path)
        {
            var data = new byte[8]; // Two stereo 16-bit silent sample frames.
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + data.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write((ushort)2);
            writer.Write(44100);
            writer.Write(44100 * 2 * 2);
            writer.Write((ushort)4);
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(data.Length);
            writer.Write(data);
        }
    }

    private sealed class FakeFlacEncoder : IFlacEncoder
    {
        private readonly int _exitCode;

        public FakeFlacEncoder(int exitCode = 0)
        {
            _exitCode = exitCode;
        }

        public List<string> EncodedInputPaths { get; } = [];

        public List<FlacEncodeOptions> EncodedOptions { get; } = [];

        public Task<FlacEncodeResult> EncodeAsync(
            FlacEncodeOptions options,
            Stream standardOutput,
            Stream standardError,
            CancellationToken cancellationToken = default)
        {
            EncodedInputPaths.Add(options.InputWavPath);
            EncodedOptions.Add(options);
            if (_exitCode == 0)
            {
                File.WriteAllText(options.OutputFlacPath, "fake flac bytes");
            }

            return Task.FromResult(new FlacEncodeResult(_exitCode));
        }
    }

    private sealed class FakeAccurateRipClient : IAccurateRipClient
    {
        private readonly Func<IReadOnlyList<(uint V1, uint V2)>, AccurateRipMatchResult>? _responder;

        public FakeAccurateRipClient(Func<IReadOnlyList<(uint V1, uint V2)>, AccurateRipMatchResult>? responder = null)
        {
            _responder = responder;
        }

        public int CallCount { get; private set; }

        public Task<AccurateRipMatchResult> MatchAsync(
            DiscToc toc,
            IReadOnlyList<(uint V1, uint V2)> computedChecksums,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_responder is not null)
            {
                return Task.FromResult(_responder(computedChecksums));
            }

            return Task.FromResult(new AccurateRipMatchResult
            {
                Found = false,
                Tracks = computedChecksums
                    .Select((c, i) => new AccurateRipTrackMatch { TrackNumber = i + 1, ComputedV1 = c.V1, ComputedV2 = c.V2 })
                    .ToList(),
            });
        }
    }
}
