using System.Linq;
using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;

namespace Whatinator.Core.Tests;

public class ReleaseFolderNamingTests
{
    [Theory]
    [InlineData(1, "cd1")]
    [InlineData(2, "cd2")]
    [InlineData(10, "cd10")]
    public void DiscFolderName_FormatsAsCdN(int discNumber, string expected)
    {
        Assert.Equal(expected, ReleaseFolderNaming.DiscFolderName(discNumber));
    }

    [Fact]
    public void ReleaseDisplayName_ExcludesYearAndFormatSuffix()
    {
        var releaseInfo = CreateReleaseInfo(date: "1992-05-12");

        Assert.Equal("Annie Lennox - Diva", ReleaseFolderNaming.ReleaseDisplayName(releaseInfo));
    }

    [Fact]
    public void ReleaseDisplayName_SanitizesForbiddenCharacters()
    {
        var releaseInfo = CreateReleaseInfo(title: "A/B: The Album");

        Assert.DoesNotContain('/', ReleaseFolderNaming.ReleaseDisplayName(releaseInfo));
        Assert.DoesNotContain(':', ReleaseFolderNaming.ReleaseDisplayName(releaseInfo));
    }

    [Theory]
    [InlineData("1992-05-12", "1992")]
    [InlineData("1992-05", "1992")]
    [InlineData("1992", "1992")]
    [InlineData(null, "0000")]
    [InlineData("", "0000")]
    [InlineData("unknown", "0000")]
    public void ExtractYear_ParsesLeadingFourDigits(string? date, string expected)
    {
        Assert.Equal(expected, ReleaseFolderNaming.ExtractYear(date));
    }

    [Theory]
    [InlineData("The Sugarcubes", "Sugarcubes, The")]
    [InlineData("the sugarcubes", "sugarcubes, the")]
    [InlineData("THE SUGARCUBES", "SUGARCUBES, THE")]
    [InlineData("Theatre of Tragedy", "Theatre of Tragedy")]
    [InlineData("Annie Lennox", "Annie Lennox")]
    [InlineData("The", "The")]
    public void SortArtist_ReordersLeadingThe(string artist, string expected)
    {
        Assert.Equal(expected, ReleaseFolderNaming.SortArtist(artist));
    }

    [Theory]
    [InlineData("flac", "Annie Lennox - Diva [flac 1992]")]
    [InlineData("mp3 v0", "Annie Lennox - Diva [mp3 v0 1992]")]
    public void ContainerFolderName_BuildsExpectedFormat(string formatTag, string expected)
    {
        var releaseInfo = CreateReleaseInfo(date: "1992-05-12");

        Assert.Equal(expected, ReleaseFolderNaming.ContainerFolderName(releaseInfo, formatTag));
    }

    [Theory]
    [InlineData("flac", "1992-05-12", "1992")]
    [InlineData("flac", "1992-05", "1992")]
    [InlineData("flac", "1992", "1992")]
    [InlineData("flac", null, "0000")]
    [InlineData("flac", "", "0000")]
    [InlineData("flac", "unknown", "0000")]
    [InlineData("mp3 v0", "1992-05-12", "1992")]
    [InlineData("mp3 v0", "1992-05", "1992")]
    [InlineData("mp3 v0", "1992", "1992")]
    [InlineData("mp3 v0", null, "0000")]
    [InlineData("mp3 v0", "", "0000")]
    [InlineData("mp3 v0", "unknown", "0000")]
    public void ContainerFolderName_ExtractsYear(string formatTag, string? date, string expectedYear)
    {
        var releaseInfo = CreateReleaseInfo(date: date);

        Assert.Contains($"[{formatTag} {expectedYear}]", ReleaseFolderNaming.ContainerFolderName(releaseInfo, formatTag));
    }

    [Theory]
    [InlineData("flac")]
    [InlineData("mp3 v0")]
    public void ContainerFolderName_SanitizesForbiddenCharacters(string formatTag)
    {
        var releaseInfo = CreateReleaseInfo(title: "A/B: The Album");

        Assert.DoesNotContain('/', ReleaseFolderNaming.ContainerFolderName(releaseInfo, formatTag));
        Assert.DoesNotContain(':', ReleaseFolderNaming.ContainerFolderName(releaseInfo, formatTag));
    }

    [Theory]
    [InlineData("flac", "Sugarcubes, The - Stick Around for Joy [flac 1992]")]
    [InlineData("mp3 v0", "Sugarcubes, The - Stick Around for Joy [mp3 v0 1992]")]
    public void ContainerFolderName_ReordersLeadingThe_ForSorting(string formatTag, string expected)
    {
        var releaseInfo = CreateReleaseInfo(date: "1992-01-01", artist: "The Sugarcubes", title: "Stick Around for Joy");

        Assert.Equal(expected, ReleaseFolderNaming.ContainerFolderName(releaseInfo, formatTag));
    }

    [Fact]
    public void ResolveDiscDirectory_MultiDisc_NestsDiscFolderUnderContainer()
    {
        var releaseInfo = CreateReleaseInfo(date: "1992-05-12", media: [1, 2]);

        var (containerDir, discDir) = ReleaseFolderNaming.ResolveDiscDirectory(releaseInfo, "/dest", "flac", discNumber: 2);

        Assert.Equal(Path.Combine("/dest", "Annie Lennox - Diva [flac 1992]"), containerDir);
        Assert.Equal(Path.Combine(containerDir, "cd2"), discDir);
    }

    [Fact]
    public void ResolveDiscDirectory_SingleDisc_DiscDirectoryIsContainerDirectory()
    {
        var releaseInfo = CreateReleaseInfo(date: "1992-05-12", media: [1]);

        var (containerDir, discDir) = ReleaseFolderNaming.ResolveDiscDirectory(releaseInfo, "/dest", "flac", discNumber: 1);

        Assert.Equal(containerDir, discDir);
    }

    private static ReleaseInfo CreateReleaseInfo(string? date = "1992-05-12", string title = "Diva", string artist = "Annie Lennox", int[]? media = null) => new(
        MusicBrainzReleaseId: "id",
        MusicBrainzUrl: "https://musicbrainz.org/release/id",
        Artist: artist,
        Title: title,
        Date: date,
        Country: null,
        Barcode: null,
        Label: null,
        CatalogNumber: null,
        Media: (media ?? []).Select(position => new MediumInfo(position, null, [])).ToList());
}
