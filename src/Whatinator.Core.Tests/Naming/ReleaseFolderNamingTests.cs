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

    private static ReleaseInfo CreateReleaseInfo(string? date = "1992-05-12", string title = "Diva") => new(
        MusicBrainzReleaseId: "id",
        MusicBrainzUrl: "https://musicbrainz.org/release/id",
        Artist: "Annie Lennox",
        Title: title,
        Date: date,
        Country: null,
        Barcode: null,
        Label: null,
        CatalogNumber: null,
        Media: []);
}
