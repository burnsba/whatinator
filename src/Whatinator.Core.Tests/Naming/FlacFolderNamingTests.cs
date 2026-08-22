using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;

namespace Whatinator.Core.Tests;

public class FlacFolderNamingTests
{
    [Fact]
    public void ContainerFolderName_BuildsExpectedFormat()
    {
        var releaseInfo = CreateReleaseInfo(date: "1992-05-12");

        Assert.Equal("Annie Lennox - Diva [flac 1992]", FlacFolderNaming.ContainerFolderName(releaseInfo));
    }

    [Theory]
    [InlineData("1992-05-12", "1992")]
    [InlineData("1992-05", "1992")]
    [InlineData("1992", "1992")]
    [InlineData(null, "0000")]
    [InlineData("", "0000")]
    [InlineData("unknown", "0000")]
    public void ContainerFolderName_ExtractsYear(string? date, string expectedYear)
    {
        var releaseInfo = CreateReleaseInfo(date: date);

        Assert.Contains($"[flac {expectedYear}]", FlacFolderNaming.ContainerFolderName(releaseInfo));
    }

    [Fact]
    public void ContainerFolderName_SanitizesForbiddenCharacters()
    {
        var releaseInfo = CreateReleaseInfo(title: "A/B: The Album");

        Assert.DoesNotContain('/', FlacFolderNaming.ContainerFolderName(releaseInfo));
        Assert.DoesNotContain(':', FlacFolderNaming.ContainerFolderName(releaseInfo));
    }

    [Fact]
    public void ContainerFolderName_ReordersLeadingThe_ForSorting()
    {
        var releaseInfo = CreateReleaseInfo(date: "1992-01-01", artist: "The Sugarcubes", title: "Stick Around for Joy");

        Assert.Equal(
            "Sugarcubes, The - Stick Around for Joy [flac 1992]",
            FlacFolderNaming.ContainerFolderName(releaseInfo));
    }

    private static ReleaseInfo CreateReleaseInfo(string? date = "1992-05-12", string title = "Diva", string artist = "Annie Lennox") => new(
        MusicBrainzReleaseId: "id",
        MusicBrainzUrl: "https://musicbrainz.org/release/id",
        Artist: artist,
        Title: title,
        Date: date,
        Country: null,
        Barcode: null,
        Label: null,
        CatalogNumber: null,
        Media: []);
}
