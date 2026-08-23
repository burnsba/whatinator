using Whatinator.Core.Checksums;
using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;

namespace Whatinator.Core.Tests;

public class MetadataUpdaterTests : IDisposable
{
    private readonly string _tempDir;

    public MetadataUpdaterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-metadataupdater-tests-" + Guid.NewGuid());
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
    public void DetectChange_NoDifference_ReportsUnchanged()
    {
        var release = CreateRelease();

        var summary = MetadataUpdater.DetectChange(release, release);

        Assert.False(summary.ArtistOrTitleChanged);
        Assert.False(summary.YearChanged);
    }

    [Fact]
    public void DetectChange_ArtistDiffers_ReportsArtistOrTitleChanged()
    {
        var oldRelease = CreateRelease(artist: "Old Artist");
        var newRelease = CreateRelease(artist: "New Artist");

        var summary = MetadataUpdater.DetectChange(oldRelease, newRelease);

        Assert.True(summary.ArtistOrTitleChanged);
        Assert.False(summary.YearChanged);
    }

    [Fact]
    public void DetectChange_TitleDiffers_ReportsArtistOrTitleChanged()
    {
        var oldRelease = CreateRelease(title: "Old Title");
        var newRelease = CreateRelease(title: "New Title");

        var summary = MetadataUpdater.DetectChange(oldRelease, newRelease);

        Assert.True(summary.ArtistOrTitleChanged);
    }

    [Fact]
    public void DetectChange_YearDiffers_ReportsYearChanged()
    {
        var oldRelease = CreateRelease(date: "2000-01-01");
        var newRelease = CreateRelease(date: "2001-01-01");

        var summary = MetadataUpdater.DetectChange(oldRelease, newRelease);

        Assert.True(summary.YearChanged);
        Assert.Equal("2000", summary.OldYear);
        Assert.Equal("2001", summary.NewYear);
        Assert.False(summary.ArtistOrTitleChanged);
    }

    [Fact]
    public void Apply_HappyPath_BacksUpWritesAndRecalculatesChecksums()
    {
        var oldRelease = CreateRelease();
        var containerDir = SetUpPackagedFolder(oldRelease, ".flac");

        var newRelease = oldRelease with { Barcode = "1234567890" };
        var result = MetadataUpdater.Apply(newRelease, containerDir);

        Assert.False(result.FolderRenamed);
        Assert.Equal(containerDir, result.FinalDirectory);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal("1234567890", ReleaseInfoFile.Load(Path.Combine(containerDir, "releaseinfo.json")).Barcode);
        Assert.Null(ReleaseInfoFile.Load(result.BackupPath).Barcode);
        Assert.True(File.Exists(Path.Combine(containerDir, "id.txt")));
        Assert.Equal(1, result.ChecksumFileCount);
        Assert.True(File.Exists(result.ChecksumFilePath));
    }

    [Fact]
    public void Apply_ThenCompareChecksum_IsCleanAndIncludesLogFile()
    {
        var oldRelease = CreateRelease();
        var containerDir = SetUpPackagedFolder(oldRelease, ".flac");
        File.WriteAllText(Path.Combine(containerDir, "Artist - Album.log"), "fake rip log");
        File.WriteAllText(Path.Combine(containerDir, "id.txt"), "fake id.txt");
        File.WriteAllText(Path.Combine(containerDir, "Artist - Album.m3u"), "fake m3u");

        var newRelease = oldRelease with { Barcode = "1234567890" };
        var result = MetadataUpdater.Apply(newRelease, containerDir);

        Assert.Equal(2, result.ChecksumFileCount); // audio + log

        var compareResult = ChecksumFile.Compare(containerDir);
        Assert.True(compareResult.IsClean);
        Assert.Empty(compareResult.Mismatched);
        Assert.Empty(compareResult.Missing);
        Assert.Contains("id.txt", compareResult.Extra);
        Assert.Contains("Artist - Album.m3u", compareResult.Extra);
        Assert.Contains("releaseinfo.json", compareResult.Extra);
    }

    [Fact]
    public void Apply_YearChanged_RenamesFolder()
    {
        var oldRelease = CreateRelease(date: "2000-01-01");
        var containerDir = SetUpPackagedFolder(oldRelease, ".flac");

        var newRelease = oldRelease with { Date = "2005-01-01" };
        var result = MetadataUpdater.Apply(newRelease, containerDir);

        Assert.True(result.FolderRenamed);
        Assert.False(Directory.Exists(containerDir));
        Assert.True(Directory.Exists(result.FinalDirectory));
        Assert.Equal("Artist - Album [flac 2005]", Path.GetFileName(result.FinalDirectory));
    }

    [Fact]
    public void Apply_Mp3Folder_UsesMp3NamingAndExtension()
    {
        var oldRelease = CreateRelease(date: "2000-01-01");
        var containerDir = SetUpPackagedFolder(oldRelease, ".mp3");

        var newRelease = oldRelease with { Date = "2005-01-01" };
        var result = MetadataUpdater.Apply(newRelease, containerDir);

        Assert.True(result.FolderRenamed);
        Assert.Equal("Artist - Album [mp3 v0 2005]", Path.GetFileName(result.FinalDirectory));
        Assert.Equal(1, result.ChecksumFileCount);
    }

    [Fact]
    public void Apply_NoExistingReleaseInfo_Throws()
    {
        var release = CreateRelease();
        Assert.Throws<FileNotFoundException>(() => MetadataUpdater.Apply(release, _tempDir));
    }

    [Fact]
    public void Apply_NoAudioFiles_Throws()
    {
        var release = CreateRelease();
        ReleaseInfoFile.Save(release, Path.Combine(_tempDir, "releaseinfo.json"));

        Assert.Throws<InvalidOperationException>(() => MetadataUpdater.Apply(release, _tempDir));
    }

    private string SetUpPackagedFolder(ReleaseInfo release, string audioExtension)
    {
        var folderName = audioExtension == ".flac"
            ? FlacFolderNaming.ContainerFolderName(release)
            : Mp3FolderNaming.ContainerFolderName(release);
        var containerDir = Path.Combine(_tempDir, folderName);
        Directory.CreateDirectory(containerDir);
        File.WriteAllText(Path.Combine(containerDir, "01. Track" + audioExtension), "fake audio");
        ReleaseInfoFile.Save(release, Path.Combine(containerDir, "releaseinfo.json"));
        return containerDir;
    }

    private static ReleaseInfo CreateRelease(string artist = "Artist", string title = "Album", string date = "2000-01-01") =>
        new(
            MusicBrainzReleaseId: "release-id",
            MusicBrainzUrl: "https://musicbrainz.org/release/release-id",
            Artist: artist,
            Title: title,
            Date: date,
            Country: "US",
            Barcode: null,
            Label: null,
            CatalogNumber: null,
            Media: [new MediumInfo(1, null, [new TrackInfo(1, "Track One", artist, TimeSpan.FromSeconds(100))])]);
}
