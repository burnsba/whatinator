using Whatinator.Core.Drive;

namespace Whatinator.Core.Tests;

public class CacheDefeatAnalyzerTests
{
    [Fact]
    public void Classify_ReturnsCanDefeat_WhenExitZeroAndOkBannerPresent()
    {
        var result = CacheDefeatAnalyzer.Classify(0, "...\nDrive tests OK with Paranoia.\n");

        Assert.Equal(CacheDefeatResult.CanDefeat, result);
    }

    [Fact]
    public void Classify_ReturnsCannotDefeat_WhenExitZeroButOkBannerMissing()
    {
        var result = CacheDefeatAnalyzer.Classify(0, "some other unexpected output\n");

        Assert.Equal(CacheDefeatResult.CannotDefeat, result);
    }

    [Fact]
    public void Classify_ReturnsCannotDefeat_WhenExitNonZeroAndWarningBannerPresent()
    {
        var result = CacheDefeatAnalyzer.Classify(1, "...\nWARNING! PARANOIA MAY NOT BE ABLE TO...\n");

        Assert.Equal(CacheDefeatResult.CannotDefeat, result);
    }

    [Fact]
    public void Classify_ReturnsCannotDefeat_WhenExitNonZeroAndAbortingBannerPresent()
    {
        var result = CacheDefeatAnalyzer.Classify(1, "...\naborting test.\n");

        Assert.Equal(CacheDefeatResult.CannotDefeat, result);
    }

    [Fact]
    public void Classify_ReturnsUnknown_WhenExitNonZeroAndNoRecognizedBanner()
    {
        var result = CacheDefeatAnalyzer.Classify(1, "cannot open disc: no medium found\n");

        Assert.Equal(CacheDefeatResult.Unknown, result);
    }

    [Fact]
    public void BuildStartInfo_PassesAnalyzeFlagAndDevice()
    {
        var startInfo = CacheDefeatAnalyzer.BuildStartInfo("/dev/sr1");

        Assert.Equal("cd-paranoia", startInfo.FileName);
        Assert.Equal(["-A", "--force-cdrom-device", "/dev/sr1"], startInfo.ArgumentList);
    }
}
