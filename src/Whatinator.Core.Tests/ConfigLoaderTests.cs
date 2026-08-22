namespace Whatinator.Core.Tests;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-config-tests-" + Guid.NewGuid());
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
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var path = Path.Combine(_tempDir, "does-not-exist.json");

        var config = ConfigLoader.Load(path);

        Assert.Equal("/dev/sr1", config.Device);
        Assert.True(config.MakeMp3);
        Assert.Null(config.ReadOffsets);
    }

    [Fact]
    public void Load_ParsesFile_WhenPresent()
    {
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, """{ "device": "/dev/sr0", "makeMp3": false, "readOffsets": { "ASUS|DRW-24F1ST|": 6 } }""");

        var config = ConfigLoader.Load(path);

        Assert.Equal("/dev/sr0", config.Device);
        Assert.False(config.MakeMp3);
        Assert.Equal(6, config.ReadOffsets?["ASUS|DRW-24F1ST|"]);
    }

    [Fact]
    public void Load_DefaultsReadOffsetsToNull_WhenFieldIsAbsent()
    {
        // Older config files (written before ReadOffsets existed) must still load cleanly.
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, """{ "device": "/dev/sr0" }""");

        var config = ConfigLoader.Load(path);

        Assert.Null(config.ReadOffsets);
    }

    [Fact]
    public void Load_ThrowsJsonException_WhenFileIsMalformed()
    {
        var path = Path.Combine(_tempDir, "config.json");
        File.WriteAllText(path, "{ not valid json");

        Assert.Throws<System.Text.Json.JsonException>(() => ConfigLoader.Load(path));
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "config.json");
        var config = new WhatinatorConfig(
            Device: "/dev/sr0",
            MakeMp3: false,
            ReadOffsets: new Dictionary<string, int> { ["ASUS|DRW-24F1ST|1.00"] = 6 });

        ConfigLoader.Save(config, path);
        var loaded = ConfigLoader.Load(path);

        Assert.Equal("/dev/sr0", loaded.Device);
        Assert.False(loaded.MakeMp3);
        Assert.Equal(6, loaded.GetReadOffset("ASUS", "DRW-24F1ST", "1.00"));
    }

    [Fact]
    public void Save_CreatesParentDirectory_WhenMissing()
    {
        var path = Path.Combine(_tempDir, "nested", "whatinator", "config.json");

        ConfigLoader.Save(new WhatinatorConfig(), path);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_Overwrites_WhenFileAlreadyExists()
    {
        var path = Path.Combine(_tempDir, "config.json");
        ConfigLoader.Save(new WhatinatorConfig(Device: "/dev/sr0"), path);

        ConfigLoader.Save(new WhatinatorConfig(Device: "/dev/sr1"), path);

        Assert.Equal("/dev/sr1", ConfigLoader.Load(path).Device);
    }

    [Fact]
    public void ResolveDefaultPath_EndsWithWhatinatorConfigJson()
    {
        var path = ConfigLoader.ResolveDefaultPath();

        Assert.EndsWith(Path.Combine("whatinator", "config.json"), path, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDefaultPath_HonorsXdgConfigHome()
    {
        var original = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        try
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _tempDir);

            var path = ConfigLoader.ResolveDefaultPath();

            Assert.Equal(Path.Combine(_tempDir, "whatinator", "config.json"), path);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", original);
        }
    }
}
