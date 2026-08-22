using Whatinator.Core.Toc;

namespace Whatinator.Core.Tests;

public class CdrdaoTocReaderTests
{
    [Fact]
    public void BuildStartInfo_UsesCdrdaoExecutable()
    {
        var startInfo = CdrdaoTocReader.BuildStartInfo("/dev/sr1", fastToc: true, "/tmp/out.toc");

        Assert.Equal("cdrdao", startInfo.FileName);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildStartInfo_FastToc_IncludesFastTocFlag()
    {
        var args = CdrdaoTocReader.BuildStartInfo("/dev/sr1", fastToc: true, "/tmp/out.toc").ArgumentList;

        Assert.Equal(["read-toc", "--fast-toc", "--device", "/dev/sr1", "/tmp/out.toc"], args);
    }

    [Fact]
    public void BuildStartInfo_FullToc_OmitsFastTocFlag()
    {
        var args = CdrdaoTocReader.BuildStartInfo("/dev/sr1", fastToc: false, "/tmp/out.toc").ArgumentList;

        Assert.Equal(["read-toc", "--device", "/dev/sr1", "/tmp/out.toc"], args);
        Assert.DoesNotContain("--fast-toc", args);
    }

    [Fact]
    public void BuildStartInfo_PassesDeviceAndOutputPath()
    {
        var args = CdrdaoTocReader.BuildStartInfo("/dev/sr0", fastToc: true, "/tmp/some-toc-file.toc").ArgumentList;

        var deviceIndex = args.IndexOf("--device");
        Assert.True(deviceIndex >= 0);
        Assert.Equal("/dev/sr0", args[deviceIndex + 1]);
        Assert.Equal("/tmp/some-toc-file.toc", args[^1]);
    }
}
