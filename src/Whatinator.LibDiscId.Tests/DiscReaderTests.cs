namespace Whatinator.LibDiscId.Tests;

#pragma warning disable CA1416 // DiscReader is [SupportedOSPlatform("linux")]; this whole test project only ever runs on Linux (see project CLAUDE.md), but isn't itself annotated.
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

    [Fact]
    public void WrapMissingLibrary_ReturnsActionableDiscIdException()
    {
        // Exercises the DllNotFoundException -> DiscIdException translation
        // directly, since there's no way to actually reproduce a missing
        // libdiscid0 on a machine that has it installed (which this dev
        // machine, and CI, both do -- see the project CLAUDE.md).
        var inner = new DllNotFoundException("Unable to load shared library 'libdiscid.so.0' or one of its dependencies.");

        var wrapped = DiscReader.WrapMissingLibrary(inner);

        Assert.False(string.IsNullOrWhiteSpace(wrapped.Message));
        Assert.Contains("libdiscid0", wrapped.Message);
        Assert.Same(inner, wrapped.InnerException);
    }
}
#pragma warning restore CA1416
