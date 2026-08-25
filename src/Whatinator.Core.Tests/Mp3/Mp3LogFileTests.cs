using Whatinator.Core.Mp3;

namespace Whatinator.Core.Tests;

public class Mp3LogFileTests
{
    [Fact]
    public void Format_MatchesExpectedLayout()
    {
        // A non-UTC offset, deliberately -- so a regression back to UtcNow
        // at a call site (see PipelineRunner/RipCommand) would shift these
        // wall-clock components and fail this test instead of passing
        // silently.
        var info = new Mp3LogInfo(
            Uname: "Linux host 6.1.0 x86_64 GNU/Linux",
            OsPrettyName: "Debian GNU/Linux 13 (trixie)",
            LameVersion: "LAME 64bits version 3.100",
            StartTime: new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.FromHours(-5)),
            EndTime: new DateTimeOffset(2026, 8, 16, 9, 5, 30, TimeSpan.FromHours(-5)));

        var expected =
            $"whatinator V{WhatinatorVersion.Current} EAC-style extraction log\n" +
            "\n" +
            "whatinator extraction logfile from 16. August 2026, 09:00\n" +
            "\n" +
            "Log created by: whatinator\n" +
            "\n" +
            "MP3 conversion phase information:\n" +
            "  OS: Linux host 6.1.0 x86_64 GNU/Linux\n" +
            "  OS (pretty name): Debian GNU/Linux 13 (trixie)\n" +
            "  Encoder: LAME 64bits version 3.100\n" +
            "  Quality: VBR -V0 (highest quality)\n" +
            "  Start time: 2026-08-16T09:00:00-05:00\n" +
            "  End time: 2026-08-16T09:05:30-05:00\n";

        Assert.Equal(expected, Mp3LogFile.Format(info));
    }

    [Fact]
    public void Format_UsesPlaceholder_WhenOsPrettyNameIsNull()
    {
        var info = new Mp3LogInfo(
            Uname: "Linux host",
            OsPrettyName: null,
            LameVersion: "LAME 3.100",
            StartTime: DateTimeOffset.UnixEpoch,
            EndTime: DateTimeOffset.UnixEpoch);

        Assert.Contains("  OS (pretty name): -\n", Mp3LogFile.Format(info), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_AppendsPerTrackSections_SeparatedByBlankLines()
    {
        List<Mp3TrackLogEntry> tracks =
        [
            new Mp3TrackLogEntry(1, 2, "Track One", "LAME 4.0 64bits\nEncoding...\n"),
            new Mp3TrackLogEntry(2, 2, "Track Two", "LAME 4.0 64bits\nEncoding...\n"),
        ];
        var info = new Mp3LogInfo(
            Uname: "Linux host",
            OsPrettyName: "Debian",
            LameVersion: "LAME 3.100",
            StartTime: DateTimeOffset.UnixEpoch,
            EndTime: DateTimeOffset.UnixEpoch,
            Tracks: tracks);

        var expected =
            "\n" +
            "Track 1 of 2: Track One\n" +
            "LAME 4.0 64bits\nEncoding...\n" +
            "\n" +
            "Track 2 of 2: Track Two\n" +
            "LAME 4.0 64bits\nEncoding...\n";

        Assert.EndsWith(expected, Mp3LogFile.Format(info), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_OmitsTrackSections_WhenTracksIsEmpty()
    {
        var info = new Mp3LogInfo(
            Uname: "Linux host",
            OsPrettyName: "Debian",
            LameVersion: "LAME 3.100",
            StartTime: DateTimeOffset.UnixEpoch,
            EndTime: DateTimeOffset.UnixEpoch,
            Tracks: []);

        Assert.DoesNotContain("Track ", Mp3LogFile.Format(info), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WritesFormattedContentToDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), "whatinator-mp3log-test-" + Guid.NewGuid() + ".log");
        try
        {
            var info = new Mp3LogInfo("Linux host", "Debian", "LAME 3.100", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

            Mp3LogFile.Write(info, path);

            Assert.Equal(Mp3LogFile.Format(info), File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
