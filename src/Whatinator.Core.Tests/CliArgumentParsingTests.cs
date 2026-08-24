using Whatinator.Core.Metadata;

namespace Whatinator.Core.Tests;

public class CliArgumentParsingTests : IDisposable
{
    private readonly string _tempDir;

    public CliArgumentParsingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-cliargumentparsing-tests-" + Guid.NewGuid());
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
    public void TryParseDiscNumber_NullArg_SucceedsWithNullDiscNumber()
    {
        var succeeded = CliArgumentParsing.TryParseDiscNumber(null, out var error, out var discNumber);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.Null(discNumber);
    }

    [Fact]
    public void TryParseDiscNumber_ValidNumber_SucceedsWithParsedValue()
    {
        var succeeded = CliArgumentParsing.TryParseDiscNumber("2", out var error, out var discNumber);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.Equal(2, discNumber);
    }

    [Fact]
    public void TryParseDiscNumber_NonNumericArg_FailsWithExpectedMessage()
    {
        var succeeded = CliArgumentParsing.TryParseDiscNumber("two", out var error, out var discNumber);

        Assert.False(succeeded);
        Assert.Equal("--disc must be a number, got 'two'.", error);
        Assert.Null(discNumber);
    }

    [Fact]
    public void TryLoadReleaseInfo_ValidFile_SucceedsWithParsedRelease()
    {
        var releaseInfo = new ReleaseInfo(
            MusicBrainzReleaseId: "13856621-72e0-4a14-b519-69513aae579f",
            MusicBrainzUrl: "https://musicbrainz.org/release/13856621-72e0-4a14-b519-69513aae579f",
            Artist: "Annie Lennox",
            Title: "Diva",
            Date: "1992-05-12",
            Country: "US",
            Barcode: "078221870429",
            Label: "Arista",
            CatalogNumber: "07822-18704-2",
            Media: [new MediumInfo(Position: 1, Subtitle: null, Tracks: [])]);
        var path = Path.Combine(_tempDir, "releaseinfo.json");
        ReleaseInfoFile.Save(releaseInfo, path);

        var succeeded = CliArgumentParsing.TryLoadReleaseInfo(path, out var loaded, out var error);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.NotNull(loaded);
        Assert.Equal("Diva", loaded.Title);
    }

    [Fact]
    public void TryLoadReleaseInfo_MissingFile_FailsWithExpectedMessageAndNoException()
    {
        var path = Path.Combine(_tempDir, "does-not-exist.json");

        var succeeded = CliArgumentParsing.TryLoadReleaseInfo(path, out var loaded, out var error);

        Assert.False(succeeded);
        Assert.Null(loaded);
        Assert.StartsWith($"Failed to read {path}: ", error);
    }

    [Fact]
    public void TryLoadReleaseInfo_MalformedJson_FailsWithExpectedMessageAndNoException()
    {
        var path = Path.Combine(_tempDir, "malformed.json");
        File.WriteAllText(path, "{ this is not valid json");

        var succeeded = CliArgumentParsing.TryLoadReleaseInfo(path, out var loaded, out var error);

        Assert.False(succeeded);
        Assert.Null(loaded);
        Assert.StartsWith($"Failed to read {path}: ", error);
    }
}
