namespace Whatinator.LibDiscId.Tests;

public class DiscReaderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Read_RejectsNullOrWhitespaceDevice(string? device)
    {
        // Null yields ArgumentNullException, empty/whitespace yields plain
        // ArgumentException -- both derive from ArgumentException.
        Assert.ThrowsAny<ArgumentException>(() => DiscReader.Read(device!));
    }

    [Fact]
    public void Read_ThrowsDiscIdExceptionForNonexistentDevice()
    {
        // No real hardware I/O is exercised here -- this path fails before
        // any device is opened, so it's safe to run in any environment.
        // The real device-read path is verified by hand against real
        // hardware in the phase 002 demo (docs/plan/demos/phase-002.md);
        // there's no practical way to unit-test actual optical drive I/O.
        var exception = Assert.Throws<DiscIdException>(
            () => DiscReader.Read("/dev/whatinator-test-nonexistent-device"));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public void GetNativeVersion_ReturnsNonEmptyString()
    {
        // Exercises the native library load path without touching any disc.
        var version = DiscReader.GetNativeVersion();

        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    [Fact]
    public void GetDefaultDevice_ReturnsNonEmptyString()
    {
        var device = DiscReader.GetDefaultDevice();

        Assert.False(string.IsNullOrWhiteSpace(device));
    }
}
