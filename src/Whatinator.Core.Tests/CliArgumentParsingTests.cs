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

    [Fact]
    public void TryResolveRetryOptions_NoArgsOrConfig_UsesHardcodedDefaults()
    {
        var succeeded = CliArgumentParsing.TryResolveRetryOptions(
            noVerify: false, null, null, null, new WhatinatorConfig(), out var error, out var maxRetries, out var maxSectorReads, out var stallTimeoutSeconds);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.Equal(5, maxRetries);
        Assert.Equal(12, maxSectorReads);
        Assert.Equal(120, stallTimeoutSeconds);
    }

    [Fact]
    public void TryResolveRetryOptions_ConfigValuesGiven_UsedWhenCliArgsAbsent()
    {
        var config = new WhatinatorConfig(MaxSectorReads: 20, StallTimeoutSeconds: 600);

        var succeeded = CliArgumentParsing.TryResolveRetryOptions(
            noVerify: false, null, null, null, config, out var error, out var maxRetries, out var maxSectorReads, out var stallTimeoutSeconds);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.Equal(5, maxRetries);
        Assert.Equal(20, maxSectorReads);
        Assert.Equal(600, stallTimeoutSeconds);
    }

    [Fact]
    public void TryResolveRetryOptions_CliArgsGiven_OverrideConfig()
    {
        var config = new WhatinatorConfig(MaxSectorReads: 20, StallTimeoutSeconds: 600);

        var succeeded = CliArgumentParsing.TryResolveRetryOptions(
            noVerify: false, "8", "0", "60", config, out var error, out var maxRetries, out var maxSectorReads, out var stallTimeoutSeconds);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.Equal(8, maxRetries);
        Assert.Equal(0, maxSectorReads);
        Assert.Equal(60, stallTimeoutSeconds);
    }

    [Theory]
    [InlineData("--retries", "notanumber", null, null)]
    [InlineData("--max-sector-reads", null, "-1", null)]
    [InlineData("--stall-timeout", null, null, "abc")]
    public void TryResolveRetryOptions_InvalidNumber_FailsWithExpectedMessage(string optionName, string? retriesArg, string? maxSectorReadsArg, string? stallTimeoutArg)
    {
        var succeeded = CliArgumentParsing.TryResolveRetryOptions(
            noVerify: false, retriesArg, maxSectorReadsArg, stallTimeoutArg, new WhatinatorConfig(), out var error, out _, out _, out _);

        Assert.False(succeeded);
        Assert.StartsWith(optionName, error);
    }

    [Fact]
    public void TryResolveRetryOptions_NoVerifyWithMaxSectorReads_Fails()
    {
        var succeeded = CliArgumentParsing.TryResolveRetryOptions(
            noVerify: true, null, "5", null, new WhatinatorConfig(), out var error, out _, out _, out _);

        Assert.False(succeeded);
        Assert.Equal("--max-sector-reads cannot be combined with --no-verify.", error);
    }

    [Fact]
    public void TryResolveRetryOptions_NoVerifyWithStallTimeout_Fails()
    {
        var succeeded = CliArgumentParsing.TryResolveRetryOptions(
            noVerify: true, null, null, "60", new WhatinatorConfig(), out var error, out _, out _, out _);

        Assert.False(succeeded);
        Assert.Equal("--stall-timeout cannot be combined with --no-verify.", error);
    }

    [Fact]
    public void TryResolveRetryOptions_NoVerifyWithoutRetryFlags_Succeeds()
    {
        var succeeded = CliArgumentParsing.TryResolveRetryOptions(
            noVerify: true, "3", null, null, new WhatinatorConfig(), out var error, out var maxRetries, out var maxSectorReads, out var stallTimeoutSeconds);

        Assert.True(succeeded);
        Assert.Null(error);
        Assert.Equal(3, maxRetries);
        Assert.Equal(12, maxSectorReads);
        Assert.Equal(120, stallTimeoutSeconds);
    }
}
