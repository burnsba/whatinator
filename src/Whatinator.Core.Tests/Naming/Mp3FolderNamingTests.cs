using Whatinator.Core.Metadata;
using Whatinator.Core.Naming;

namespace Whatinator.Core.Tests;

public class Mp3FolderNamingTests
{
    [Fact]
    public void ContainerFolderName_BuildsExpectedFormat()
    {
        var releaseInfo = CreateReleaseInfo(date: "1992-05-12");

        Assert.Equal("Annie Lennox - Diva [mp3 v0 1992]", Mp3FolderNaming.ContainerFolderName(releaseInfo));
    }

    [Fact]
    public void ContainerFolderName_SanitizesForbiddenCharacters()
    {
        var releaseInfo = CreateReleaseInfo(title: "A/B: The Album");

        Assert.DoesNotContain('/', Mp3FolderNaming.ContainerFolderName(releaseInfo));
        Assert.DoesNotContain(':', Mp3FolderNaming.ContainerFolderName(releaseInfo));
    }

    [Fact]
    public void ContainerFolderName_ReordersLeadingThe_ForSorting()
    {
        var releaseInfo = CreateReleaseInfo(date: "1992-01-01", artist: "The Sugarcubes", title: "Stick Around for Joy");

        Assert.Equal(
            "Sugarcubes, The - Stick Around for Joy [mp3 v0 1992]",
            Mp3FolderNaming.ContainerFolderName(releaseInfo));
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
