using System.Text.Json;
using System.Text.Json.Serialization;
using Whatinator.Core.Drive;

namespace Whatinator.Core.Tests;

public class WhatinatorConfigTests : IDisposable
{
    private readonly string _tempDir;

    public WhatinatorConfigTests()
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
    public void DriveKey_CombinesVendorModelRelease()
    {
        Assert.Equal("ASUS|DRW-24F1ST|1.00", WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST", "1.00"));
    }

    [Fact]
    public void DriveKey_TwoDifferentDrives_DoNotCollide()
    {
        var first = WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST", null);
        var second = WhatinatorConfig.DriveKey("HL-DT-ST", "DVDRAM GH24NSC0", null);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GetReadOffset_ReturnsNull_WhenMapIsNull()
    {
        var config = new WhatinatorConfig();

        Assert.Null(config.GetReadOffset("ASUS", "DRW-24F1ST"));
    }

    [Fact]
    public void GetReadOffset_ReturnsNull_WhenDriveHasNoEntry()
    {
        var config = new WhatinatorConfig(ReadOffsets: new Dictionary<string, int>
        {
            [WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST")] = 6,
        });

        Assert.Null(config.GetReadOffset("HL-DT-ST", "DVDRAM GH24NSC0"));
    }

    [Fact]
    public void GetReadOffset_ReturnsTheConfiguredValue_ForAMatchingDrive()
    {
        var config = new WhatinatorConfig(ReadOffsets: new Dictionary<string, int>
        {
            [WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST")] = 6,
        });

        Assert.Equal(6, config.GetReadOffset("ASUS", "DRW-24F1ST"));
    }

    [Fact]
    public void ReadOffsets_RoundTripsThroughConfigLoader_WithTwoDrivesNotColliding()
    {
        var path = Path.Combine(_tempDir, "config.json");
        var original = new WhatinatorConfig(ReadOffsets: new Dictionary<string, int>
        {
            [WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST")] = 6,
            [WhatinatorConfig.DriveKey("HL-DT-ST", "DVDRAM GH24NSC0")] = -30,
        });

        File.WriteAllText(path, JsonSerializer.Serialize(original, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));

        var loaded = ConfigLoader.Load(path);

        Assert.Equal(6, loaded.GetReadOffset("ASUS", "DRW-24F1ST"));
        Assert.Equal(-30, loaded.GetReadOffset("HL-DT-ST", "DVDRAM GH24NSC0"));
    }

    [Fact]
    public void EffectiveUserAgent_DefaultsToWhatinatorUserAgentDefault_ContainingCurrentVersion()
    {
        var config = new WhatinatorConfig();

        Assert.Contains(WhatinatorVersion.Current, config.EffectiveUserAgent, StringComparison.Ordinal);
        Assert.DoesNotContain("whatinator/0.1 (", config.EffectiveUserAgent, StringComparison.Ordinal);
        Assert.Equal(WhatinatorUserAgent.Default, config.EffectiveUserAgent);
    }

    [Fact]
    public void EffectiveUserAgent_ReturnsConfiguredValue_WhenSet()
    {
        var config = new WhatinatorConfig(UserAgent: "custom-agent/1.0 ( me@example.com )");

        Assert.Equal("custom-agent/1.0 ( me@example.com )", config.EffectiveUserAgent);
    }

    [Fact]
    public void GetCacheDefeat_ReturnsUnknown_WhenMapIsNull()
    {
        var config = new WhatinatorConfig();

        Assert.Equal(CacheDefeatResult.Unknown, config.GetCacheDefeat("ASUS", "DRW-24F1ST"));
    }

    [Fact]
    public void GetCacheDefeat_ReturnsUnknown_WhenDriveHasNoEntry()
    {
        var config = new WhatinatorConfig(CacheDefeats: new Dictionary<string, CacheDefeatResult>
        {
            [WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST")] = CacheDefeatResult.CanDefeat,
        });

        Assert.Equal(CacheDefeatResult.Unknown, config.GetCacheDefeat("HL-DT-ST", "DVDRAM GH24NSC0"));
    }

    [Fact]
    public void GetCacheDefeat_ReturnsTheConfiguredValue_ForAMatchingDrive()
    {
        var config = new WhatinatorConfig(CacheDefeats: new Dictionary<string, CacheDefeatResult>
        {
            [WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST")] = CacheDefeatResult.CanDefeat,
        });

        Assert.Equal(CacheDefeatResult.CanDefeat, config.GetCacheDefeat("ASUS", "DRW-24F1ST"));
    }

    [Fact]
    public void CacheDefeats_RoundTripsThroughConfigLoader_AsReadableStrings()
    {
        var path = Path.Combine(_tempDir, "config.json");
        var original = new WhatinatorConfig(CacheDefeats: new Dictionary<string, CacheDefeatResult>
        {
            [WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST")] = CacheDefeatResult.CanDefeat,
        });

        File.WriteAllText(path, JsonSerializer.Serialize(original, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        }));

        Assert.Contains("CanDefeat", File.ReadAllText(path), StringComparison.Ordinal);

        var loaded = ConfigLoader.Load(path);

        Assert.Equal(CacheDefeatResult.CanDefeat, loaded.GetCacheDefeat("ASUS", "DRW-24F1ST"));
    }

    [Fact]
    public void GetOverread_ReturnsFalse_WhenMapIsNull()
    {
        var config = new WhatinatorConfig();

        Assert.False(config.GetOverread("ASUS", "DRW-24F1ST"));
    }

    [Fact]
    public void GetOverread_ReturnsFalse_WhenDriveHasNoEntry()
    {
        var config = new WhatinatorConfig(Overreads: new Dictionary<string, bool>
        {
            [WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST")] = true,
        });

        Assert.False(config.GetOverread("HL-DT-ST", "DVDRAM GH24NSC0"));
    }

    [Fact]
    public void GetOverread_ReturnsTheConfiguredValue_ForAMatchingDrive()
    {
        var config = new WhatinatorConfig(Overreads: new Dictionary<string, bool>
        {
            [WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST")] = true,
        });

        Assert.True(config.GetOverread("ASUS", "DRW-24F1ST"));
    }

    [Fact]
    public void Overreads_RoundTripsThroughConfigLoader()
    {
        var path = Path.Combine(_tempDir, "config.json");
        var original = new WhatinatorConfig(Overreads: new Dictionary<string, bool>
        {
            [WhatinatorConfig.DriveKey("ASUS", "DRW-24F1ST")] = true,
        });

        File.WriteAllText(path, JsonSerializer.Serialize(original, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }));

        var loaded = ConfigLoader.Load(path);

        Assert.True(loaded.GetOverread("ASUS", "DRW-24F1ST"));
    }
}
