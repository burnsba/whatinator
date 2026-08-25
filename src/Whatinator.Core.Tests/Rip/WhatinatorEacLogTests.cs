using System.Security.Cryptography;
using System.Text;
using Whatinator.Core.AccurateRip;
using Whatinator.Core.Drive;
using Whatinator.Core.Metadata;
using Whatinator.Core.Rip;
using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class WhatinatorEacLogTests
{
    // A non-UTC offset, deliberately -- so a regression back to UtcNow at a
    // call site (see PipelineRunner/RipCommand) would shift these wall-clock
    // components and fail Format_IncludesHeaderDriveAndSettingsSections
    // instead of passing silently.
    private static readonly DateTimeOffset StartTime = new(2026, 8, 17, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset EndTime = new(2026, 8, 17, 9, 12, 30, TimeSpan.FromHours(-5));

    [Fact]
    public void Format_IncludesHeaderDriveAndSettingsSections()
    {
        var text = WhatinatorEacLog.Format(CreateOptions());

        Assert.Contains($"whatinator V{WhatinatorVersion.Current} EAC-style extraction log\n", text, StringComparison.Ordinal);
        Assert.Contains("whatinator extraction logfile from 17. August 2026, 09:00\n", text, StringComparison.Ordinal);
        Assert.Contains("Artist / Album\n", text, StringComparison.Ordinal);
        Assert.Contains("OS: Linux host\n", text, StringComparison.Ordinal);
        Assert.Contains("OS (pretty name): Debian GNU/Linux 13 (trixie)\n", text, StringComparison.Ordinal);
        Assert.Contains("Used drive  : ASUS DRW-24F1ST   b (revision 1.00)   Device: /dev/sr1\n", text, StringComparison.Ordinal);
        Assert.Contains("Disc catalogue number (UPC/EAN)             : none\n", text, StringComparison.Ordinal);
        Assert.Contains("Read mode : Secure\n", text, StringComparison.Ordinal);
        Assert.Contains("Read offset correction                      : 6\n", text, StringComparison.Ordinal);
        Assert.Contains("Overread into Lead-In and Lead-Out          : No\n", text, StringComparison.Ordinal);
        Assert.Contains(
            "Used interface                              : cdparanoia III release 10.2 libcdio 2.2.0 x86_64-pc-linux-gnu (libcdio-paranoia)\n",
            text,
            StringComparison.Ordinal);
        Assert.Contains(
            "Gap detection                               : Cdrdao version 1.2.6 - (C) Andreas Mueller <andreas@daneb.de>\n",
            text,
            StringComparison.Ordinal);
        Assert.Contains("Defeat audio cache                          : Yes\n", text, StringComparison.Ordinal);
        Assert.Contains("Used output format              : FLAC\n", text, StringComparison.Ordinal);
        Assert.Contains("Command line compressor         : flac 1.5.0\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_TocTable_MatchesEacColumnLayout()
    {
        var text = WhatinatorEacLog.Format(CreateOptions());

        Assert.Contains("     Track |   Start  |  Length  | Start sector | End sector \n", text, StringComparison.Ordinal);
        Assert.Contains("    ---------------------------------------------------------\n", text, StringComparison.Ordinal);

        // Track 1: TOC frames 150-16627 (pregap 150), pregap-inclusive start = 0,
        // length = 16628 frames = 221s + 53 frames = 3:41.53.
        Assert.Contains("        1  |  0:00.00 |  3:41.53 |         0    |    16627   \n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesPercentagePeakFormat_NotBareFractionFormat()
    {
        var text = WhatinatorEacLog.Format(CreateOptions());

        // Peak 16384 / 32768 * 100 = 50.0% -- EAC's percentage convention,
        // not a bare 0.NNNNNN fraction (this project's other prior-research
        // conventions used that format instead -- see root CLAUDE.md § Gotchas).
        Assert.Contains("     Peak level 50.0 %\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("0.500000", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesPercentageQualityFormat_ForKnownQuality()
    {
        var trackResult = new WhatinatorTrackRipResult(
            TrackNumber: 1,
            Degraded: false,
            FlacFilePath: "/scratch/01 - Track One.flac",
            WavFilePath: null,
            Crc32: 0x828BDB5E,
            Peak: 16384,
            Quality: 0.999,
            Attempts: 1,
            AccurateRip: new AccurateRipTrackMatch { TrackNumber = 1, ComputedV1 = 1, ComputedV2 = 2 },
            ElapsedTime: TimeSpan.FromSeconds(13.75));
        var ripResult = new WhatinatorRipResult([trackResult], AccurateRipFound: false, SkippedDataTrackCount: 0);
        var options = CreateOptions() with { RipResult = ripResult };

        var text = WhatinatorEacLog.Format(options);

        // Matches the real EAC log's own example value (example/Bob Dylan -
        // Desire.log's "Track quality 99.9 %").
        Assert.Contains("     Track quality 99.9 %\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_UsesNotAvailable_ForNullQuality()
    {
        var trackResult = new WhatinatorTrackRipResult(
            TrackNumber: 1,
            Degraded: false,
            FlacFilePath: "/scratch/01 - Track One.flac",
            WavFilePath: null,
            Crc32: 0x828BDB5E,
            Peak: 16384,
            Quality: null,
            Attempts: 1,
            AccurateRip: new AccurateRipTrackMatch { TrackNumber = 1, ComputedV1 = 1, ComputedV2 = 2 },
            ElapsedTime: TimeSpan.FromSeconds(13.75));
        var ripResult = new WhatinatorRipResult([trackResult], AccurateRipFound: false, SkippedDataTrackCount: 0);
        var options = CreateOptions() with { RipResult = ripResult };

        var text = WhatinatorEacLog.Format(options);

        Assert.Contains("     Track quality not available\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_AccurateRipHit_UsesConfidencePhrasing()
    {
        var match = new AccurateRipTrackMatch
        {
            TrackNumber = 1,
            ComputedV1 = 1,
            ComputedV2 = 2,
            MatchedCrcV2 = "E4DA7C28",
            ConfidenceV2 = 2,
        };
        var text = WhatinatorEacLog.Format(CreateOptions(accurateRipFound: true, match: match));

        Assert.Contains("     Accurately ripped (confidence 2)  [E4DA7C28]  (AR v2)\n", text, StringComparison.Ordinal);
        Assert.Contains("All tracks accurately ripped\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_AccurateRipHit_V1Only_UsesV1Label()
    {
        var match = new AccurateRipTrackMatch
        {
            TrackNumber = 1,
            ComputedV1 = 1,
            ComputedV2 = 2,
            MatchedCrcV1 = "441B6D23",
            ConfidenceV1 = 15,
        };
        var text = WhatinatorEacLog.Format(CreateOptions(accurateRipFound: true, match: match));

        Assert.Contains("     Accurately ripped (confidence 15)  [441B6D23]  (AR v1)\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_AccurateRipHit_ComputedV1EqualsComputedV2_OmitsVersionLabel()
    {
        // The version label is only knowable because a match against just
        // one of computed.V1/computed.V2 is (up to a ~2^-32 coincidence) a
        // match against that specific algorithm's output. When the two
        // computed values happen to coincide for this track, that inference
        // breaks down -- see AccurateRipClient.MatchTrack and
        // FormatAccurateRipLine.
        var match = new AccurateRipTrackMatch
        {
            TrackNumber = 1,
            ComputedV1 = 42,
            ComputedV2 = 42,
            MatchedCrcV1 = "0000002A",
            ConfidenceV1 = 10,
            MatchedCrcV2 = "0000002A",
            ConfidenceV2 = 10,
        };
        var text = WhatinatorEacLog.Format(CreateOptions(accurateRipFound: true, match: match));

        Assert.Contains("     Accurately ripped (confidence 10)  [0000002A]\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(AR v1)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("(AR v2)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NoAccurateRipHit_UsesTrackNotPresentPhrasing()
    {
        var text = WhatinatorEacLog.Format(CreateOptions(accurateRipFound: false, match: null));

        Assert.Contains("     Track not present in AccurateRip database\n", text, StringComparison.Ordinal);
        Assert.Contains("None of the tracks are present in the AccurateRip database\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_DegradedTrack_ShowsWarningInsteadOfCrashing()
    {
        var toc = new DiscToc([new DiscTocTrack(1, 150, 16627, IsAudio: true, PregapFrames: 150)]);
        var degradedTrack = new WhatinatorTrackRipResult(1, true, null, null, null, null, null, 5);
        var ripResult = new WhatinatorRipResult([degradedTrack], AccurateRipFound: false, SkippedDataTrackCount: 0);
        var options = CreateOptions() with { RipResult = ripResult, Toc = toc };

        var text = WhatinatorEacLog.Format(options);

        Assert.Contains("     [WARNING] Track could not be read after 5 attempt(s) - no data captured\n", text, StringComparison.Ordinal);
        Assert.Contains("Some tracks were not ripped (skipped)\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NonDegradedTrackOnDegradedDisc_ShowsSkippedAccurateRipLine()
    {
        // WhatinatorRipRunner never attempts the whole-disc AccurateRip
        // lookup when any track on the disc is Degraded (it needs a
        // checksum for every audio track) -- so a *different* track on the
        // same disc still has AccurateRip == null even though it read fine.
        var toc = new DiscToc(
        [
            new DiscTocTrack(1, 150, 16627, IsAudio: true, PregapFrames: 150),
            new DiscTocTrack(2, 16628, 33000, IsAudio: true),
        ]);
        var goodTrack = new WhatinatorTrackRipResult(1, false, "/scratch/01.flac", null, 0x828BDB5E, 16384, 1.0, 1, AccurateRip: null);
        var degradedTrack = new WhatinatorTrackRipResult(2, true, null, null, null, null, null, 5);
        var ripResult = new WhatinatorRipResult([goodTrack, degradedTrack], AccurateRipFound: false, SkippedDataTrackCount: 0);
        var options = CreateOptions() with { RipResult = ripResult, Toc = toc };

        var text = WhatinatorEacLog.Format(options);

        Assert.Contains(
            "     AccurateRip verification skipped (one or more tracks on this disc could not be read)\n",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Format_OmitsPregapLine_WhenZero()
    {
        var toc = new DiscToc([new DiscTocTrack(1, 0, 16477, IsAudio: true, PregapFrames: null)]);
        var options = CreateOptions() with { Toc = toc };

        var text = WhatinatorEacLog.Format(options);

        Assert.DoesNotContain("Pre-gap length", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesCatalogNumber_WhenPresent()
    {
        var toc = new DiscToc([new DiscTocTrack(1, 150, 16627, IsAudio: true, PregapFrames: 150)], CatalogNumber: "602475160991");
        var options = CreateOptions() with { Toc = toc };

        var text = WhatinatorEacLog.Format(options);

        Assert.Contains("Disc catalogue number (UPC/EAN)             : 602475160991\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IncludesIsrcLine_WhenTrackHasOne()
    {
        var toc = new DiscToc([new DiscTocTrack(1, 150, 16627, IsAudio: true, PregapFrames: 150, Isrc: "USRC17607839")]);
        var options = CreateOptions() with { Toc = toc };

        var text = WhatinatorEacLog.Format(options);

        Assert.Contains("     ISRC USRC17607839\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_OmitsIsrcLine_WhenTrackHasNone()
    {
        var text = WhatinatorEacLog.Format(CreateOptions());

        Assert.DoesNotContain("ISRC ", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_NeverContainsCueToolsText()
    {
        var text = WhatinatorEacLog.Format(CreateOptions());

        Assert.DoesNotContain("CUETools", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CTDB", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_TrailingChecksumLine_ValidatesAgainstPrecedingText()
    {
        var text = WhatinatorEacLog.Format(CreateOptions());

        var lastNewline = text.TrimEnd('\n').LastIndexOf('\n');
        var bodyEnd = lastNewline + 1;
        var body = text[..bodyEnd];
        var footer = text[bodyEnd..].Trim();

        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        Assert.Equal($"==== Log checksum {expectedHash} ====", footer);
    }

    [Fact]
    public void Write_WritesFormattedContentToDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), "whatinator-eaclog-test-" + Guid.NewGuid() + ".log");
        try
        {
            var options = CreateOptions();

            WhatinatorEacLog.Write(options, path);

            Assert.Equal(WhatinatorEacLog.Format(options), File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void FormatSpeed_ReturnsMultipleOfRealtime_ForKnownDurationAndElapsed()
    {
        // 75 audio frames = exactly 1 second of track; read in 1/16th of a
        // second -- speed is EAC's own single-read-drive-speed meaning (see
        // CdParanoiaTrackResult.ElapsedTime), so this pins that a 16x read
        // reports "16.0 X" regardless of how many retries or how long the
        // copy read/sox analysis afterward took.
        var tocTrack = new DiscTocTrack(1, 0, 74, IsAudio: true, PregapFrames: 0);
        var track = CreateTrackResult(ElapsedTime: TimeSpan.FromSeconds(1.0 / 16));

        Assert.Equal("16.0 X", WhatinatorEacLog.FormatSpeed(track, tocTrack));
    }

    [Fact]
    public void FormatSpeed_ReturnsPlaceholder_WhenElapsedTimeIsNull()
    {
        var tocTrack = new DiscTocTrack(1, 0, 74, IsAudio: true, PregapFrames: 0);
        var track = CreateTrackResult(ElapsedTime: null);

        Assert.Equal("-", WhatinatorEacLog.FormatSpeed(track, tocTrack));
    }

    [Fact]
    public void FormatSpeed_ReturnsPlaceholder_WhenElapsedTimeIsZero()
    {
        var tocTrack = new DiscTocTrack(1, 0, 74, IsAudio: true, PregapFrames: 0);
        var track = CreateTrackResult(ElapsedTime: TimeSpan.Zero);

        Assert.Equal("-", WhatinatorEacLog.FormatSpeed(track, tocTrack));
    }

    private static WhatinatorTrackRipResult CreateTrackResult(TimeSpan? ElapsedTime) => new(
        TrackNumber: 1,
        Degraded: false,
        FlacFilePath: "/scratch/01 - Track One.flac",
        WavFilePath: null,
        Crc32: 0x828BDB5E,
        Peak: 16384,
        Quality: 1.0,
        Attempts: 1,
        ElapsedTime: ElapsedTime);

    private static EacLogOptions CreateOptions(bool accurateRipFound = false, AccurateRipTrackMatch? match = null)
    {
        var toc = new DiscToc([new DiscTocTrack(1, 150, 16627, IsAudio: true, PregapFrames: 150)]);

        var trackResult = new WhatinatorTrackRipResult(
            TrackNumber: 1,
            Degraded: false,
            FlacFilePath: "/scratch/01 - Track One.flac",
            WavFilePath: null,
            Crc32: 0x828BDB5E,
            Peak: 16384,
            Quality: 1.0,
            Attempts: 1,
            AccurateRip: match ?? new AccurateRipTrackMatch { TrackNumber = 1, ComputedV1 = 1, ComputedV2 = 2 },
            ElapsedTime: TimeSpan.FromSeconds(13.75));

        var ripResult = new WhatinatorRipResult([trackResult], accurateRipFound, SkippedDataTrackCount: 0);

        var releaseInfo = new ReleaseInfo(
            MusicBrainzReleaseId: "release-id",
            MusicBrainzUrl: "https://musicbrainz.org/release/release-id",
            Artist: "Artist",
            Title: "Album",
            Date: "2000-01-01",
            Country: "US",
            Barcode: null,
            Label: null,
            CatalogNumber: null,
            Media: [new MediumInfo(1, null, [new TrackInfo(1, "Track One", "Artist", TimeSpan.FromSeconds(220))])]);

        return new EacLogOptions(
            ReleaseInfo: releaseInfo,
            RipResult: ripResult,
            Toc: toc,
            DiscDirectory: "/out/Artist - Album [flac 2000]",
            DevicePath: "/dev/sr1",
            DriveVendor: "ASUS",
            DriveModel: "DRW-24F1ST   b",
            DriveRelease: "1.00",
            ReadOffset: 6,
            Overread: false,
            CacheDefeat: CacheDefeatResult.CanDefeat,
            CdParanoiaVersion: "cdparanoia III release 10.2 libcdio 2.2.0 x86_64-pc-linux-gnu",
            CdrdaoVersion: "Cdrdao version 1.2.6 - (C) Andreas Mueller <andreas@daneb.de>",
            FlacVersion: "flac 1.5.0",
            Uname: "Linux host",
            OsPrettyName: "Debian GNU/Linux 13 (trixie)",
            StartTime: StartTime,
            EndTime: EndTime);
    }
}
