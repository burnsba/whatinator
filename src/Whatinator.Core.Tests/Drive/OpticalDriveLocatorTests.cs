using Whatinator.Core.Drive;

namespace Whatinator.Core.Tests;

public class OpticalDriveLocatorTests : IDisposable
{
    private readonly string _sysClassBlockPath;

    public OpticalDriveLocatorTests()
    {
        _sysClassBlockPath = Path.Combine(Path.GetTempPath(), "whatinator-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_sysClassBlockPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sysClassBlockPath))
        {
            Directory.Delete(_sysClassBlockPath, recursive: true);
        }
    }

    [Fact]
    public void Enumerate_ReturnsEmpty_WhenPathDoesNotExist()
    {
        var result = OpticalDriveLocator.Enumerate(Path.Combine(_sysClassBlockPath, "does-not-exist"));

        Assert.Empty(result);
    }

    [Fact]
    public void Enumerate_FindsOnlyOpticalDrives_OrderedByDevicePath()
    {
        CreateDevice("sda", vendor: "Samsung", model: "SSD 990");
        CreateDevice("sr1", vendor: "ASUS", model: "DRW-24F1ST   b");
        CreateDevice("sr0", vendor: "HL-DT-ST", model: "BD-RE  WH16NS60");
        CreateDevice("sr10", vendor: "Fake", model: "TripleDigitEdgeCase");

        var result = OpticalDriveLocator.Enumerate(_sysClassBlockPath);

        Assert.Equal(["/dev/sr0", "/dev/sr1", "/dev/sr10"], result.Select(d => d.DevicePath));
    }

    [Fact]
    public void Enumerate_TrimsVendorAndModel()
    {
        CreateDevice("sr1", vendor: "ASUS    \n", model: "DRW-24F1ST   b  \n");

        var result = OpticalDriveLocator.Enumerate(_sysClassBlockPath);

        var drive = Assert.Single(result);
        Assert.Equal("ASUS", drive.Vendor);
        Assert.Equal("DRW-24F1ST   b", drive.Model);
    }

    [Fact]
    public void Enumerate_ToleratesMissingVendorAndModelFiles()
    {
        Directory.CreateDirectory(Path.Combine(_sysClassBlockPath, "sr1", "device"));

        var result = OpticalDriveLocator.Enumerate(_sysClassBlockPath);

        var drive = Assert.Single(result);
        Assert.Equal("/dev/sr1", drive.DevicePath);
        Assert.Null(drive.Vendor);
        Assert.Null(drive.Model);
        Assert.Null(drive.Release);
    }

    [Fact]
    public void Enumerate_ReadsAndTrimsRelease()
    {
        CreateDevice("sr1", vendor: "ASUS", model: "DRW-24F1ST   b", release: "1.00\n");

        var result = OpticalDriveLocator.Enumerate(_sysClassBlockPath);

        var drive = Assert.Single(result);
        Assert.Equal("1.00", drive.Release);
    }

    private void CreateDevice(string name, string vendor, string model, string? release = null)
    {
        var deviceDir = Path.Combine(_sysClassBlockPath, name, "device");
        Directory.CreateDirectory(deviceDir);
        File.WriteAllText(Path.Combine(deviceDir, "vendor"), vendor);
        File.WriteAllText(Path.Combine(deviceDir, "model"), model);
        if (release is not null)
        {
            File.WriteAllText(Path.Combine(deviceDir, "rev"), release);
        }
    }
}
