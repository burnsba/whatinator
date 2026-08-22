namespace Whatinator.Core.Tests;

public class SystemInfoTests : IDisposable
{
    private readonly string _tempDir;

    public SystemInfoTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-systeminfo-tests-" + Guid.NewGuid());
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
    public void GetOsPrettyName_ParsesQuotedValue()
    {
        var path = Path.Combine(_tempDir, "os-release");
        File.WriteAllText(path, "NAME=\"Debian GNU/Linux\"\nPRETTY_NAME=\"Debian GNU/Linux 13 (trixie)\"\nVERSION_ID=\"13\"\n");

        Assert.Equal("Debian GNU/Linux 13 (trixie)", SystemInfo.GetOsPrettyName(path));
    }

    [Fact]
    public void GetOsPrettyName_ReturnsNull_WhenFieldMissing()
    {
        var path = Path.Combine(_tempDir, "os-release");
        File.WriteAllText(path, "NAME=\"Debian GNU/Linux\"\n");

        Assert.Null(SystemInfo.GetOsPrettyName(path));
    }

    [Fact]
    public void GetOsPrettyName_ReturnsNull_WhenFileMissing()
    {
        Assert.Null(SystemInfo.GetOsPrettyName(Path.Combine(_tempDir, "does-not-exist")));
    }

    [Fact]
    public void GetUname_ReturnsNonEmptyOutput()
    {
        Assert.False(string.IsNullOrWhiteSpace(SystemInfo.GetUname()));
        Assert.NotEqual("unknown", SystemInfo.GetUname());
    }

    [Fact]
    public void GetLameVersion_ReturnsNonEmptyOutput()
    {
        Assert.False(string.IsNullOrWhiteSpace(SystemInfo.GetLameVersion()));
        Assert.NotEqual("unknown", SystemInfo.GetLameVersion());
    }

    [Fact]
    public void GetFlacVersion_ReturnsNonEmptyOutput()
    {
        Assert.False(string.IsNullOrWhiteSpace(SystemInfo.GetFlacVersion()));
        Assert.NotEqual("unknown", SystemInfo.GetFlacVersion());
    }

    [Fact]
    public void GetCdParanoiaVersion_ReturnsNonEmptyOutput()
    {
        // cd-paranoia writes its version banner to stderr, not stdout.
        Assert.False(string.IsNullOrWhiteSpace(SystemInfo.GetCdParanoiaVersion()));
        Assert.NotEqual("unknown", SystemInfo.GetCdParanoiaVersion());
    }

    [Fact]
    public void GetCdrdaoVersion_ReturnsNonEmptyOutput()
    {
        // Bare cdrdao exits 1 (no command given) but still prints its
        // version banner as the first line of stderr.
        Assert.False(string.IsNullOrWhiteSpace(SystemInfo.GetCdrdaoVersion()));
        Assert.NotEqual("unknown", SystemInfo.GetCdrdaoVersion());
        Assert.StartsWith("Cdrdao version", SystemInfo.GetCdrdaoVersion(), StringComparison.Ordinal);
    }
}
