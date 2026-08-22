using System.Diagnostics;
using Whatinator.Core.CoverArt;

namespace Whatinator.Core.Tests;

/// <summary>
/// Exercises <see cref="CoverArtProcessor"/> against the real <c>magick</c>
/// binary (available on the dev machine) rather than mocking it -- the whole
/// point of these tests is to verify actual ImageMagick behavior (resize
/// geometry, format conversion), which a fake process couldn't meaningfully
/// stand in for.
/// </summary>
public class CoverArtProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ConvertsOversizedLosslessPngToResizedJpeg()
    {
        var png = CreateSyntheticImage(".png", 3000, 2000);

        var result = await CoverArtProcessor.ProcessAsync(new CoverArtResult(png, ".png"));

        Assert.Equal(".jpg", result.FileExtension);
        var (width, height, format) = Identify(result.Content, ".jpg");
        Assert.Equal("JPEG", format);
        Assert.True(width <= 1920 && height <= 1080, $"expected to fit 1920x1080, got {width}x{height}");
        Assert.True(width == 1920 || height == 1080, "expected the image to be scaled to fill one bound exactly");
    }

    [Fact]
    public async Task ProcessAsync_ConvertsSmallLosslessPngToJpegWithoutResizing()
    {
        var png = CreateSyntheticImage(".png", 500, 400);

        var result = await CoverArtProcessor.ProcessAsync(new CoverArtResult(png, ".png"));

        Assert.Equal(".jpg", result.FileExtension);
        var (width, height, format) = Identify(result.Content, ".jpg");
        Assert.Equal("JPEG", format);
        Assert.Equal(500, width);
        Assert.Equal(400, height);
    }

    [Fact]
    public async Task ProcessAsync_ResizesOversizedJpegButKeepsItAsJpeg()
    {
        var jpeg = CreateSyntheticImage(".jpg", 3000, 2000);

        var result = await CoverArtProcessor.ProcessAsync(new CoverArtResult(jpeg, ".jpg"));

        Assert.Equal(".jpg", result.FileExtension);
        var (width, height, format) = Identify(result.Content, ".jpg");
        Assert.Equal("JPEG", format);
        Assert.True(width <= 1920 && height <= 1080, $"expected to fit 1920x1080, got {width}x{height}");
    }

    [Fact]
    public async Task ProcessAsync_LeavesSmallJpegDimensionsUnchanged()
    {
        var jpeg = CreateSyntheticImage(".jpg", 500, 400);

        var result = await CoverArtProcessor.ProcessAsync(new CoverArtResult(jpeg, ".jpg"));

        var (width, height, format) = Identify(result.Content, ".jpg");
        Assert.Equal("JPEG", format);
        Assert.Equal(500, width);
        Assert.Equal(400, height);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsOriginalUnchanged_WhenInputIsNotAValidImage()
    {
        var original = new CoverArtResult([1, 2, 3, 4], ".png");

        var result = await CoverArtProcessor.ProcessAsync(original);

        Assert.Same(original, result);
    }

    private static byte[] CreateSyntheticImage(string extension, int width, int height)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);
        try
        {
            RunMagick(["-size", $"{width}x{height}", "xc:red", path]);
            return File.ReadAllBytes(path);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static (int Width, int Height, string Format) Identify(byte[] content, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);
        try
        {
            File.WriteAllBytes(path, content);
            var output = RunMagick(["identify", "-format", "%w %h %m", path]);
            var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return (int.Parse(parts[0]), int.Parse(parts[1]), parts[2]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string RunMagick(string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "magick",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) !;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
