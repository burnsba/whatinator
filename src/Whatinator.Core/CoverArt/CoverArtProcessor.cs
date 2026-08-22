using System.ComponentModel;
using System.Diagnostics;

namespace Whatinator.Core.CoverArt;

/// <summary>
/// Shrinks cover art via ImageMagick: converts lossless formats (PNG, GIF,
/// BMP, TIFF) to JPEG at 90% quality, and scales anything larger than
/// 1920x1080 in either dimension down to fit (preserving aspect ratio,
/// never upscaling). Best-effort -- if ImageMagick isn't available or
/// processing fails for any reason, the original image is returned
/// unchanged rather than losing cover art entirely.
/// </summary>
public static class CoverArtProcessor
{
    /// <summary>The maximum width/height a cover image is scaled down to fit within.</summary>
    private const string MaxDimensions = "1920x1080>";

    /// <summary>The JPEG quality used when re-encoding a lossless source image.</summary>
    private const string JpegQuality = "90";

    /// <summary>File extensions (case-insensitive) treated as lossless and converted to JPEG.</summary>
    private static readonly HashSet<string> LosslessExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".gif", ".bmp", ".tif", ".tiff",
    };

    /// <summary>Processes a downloaded cover art image, if possible.</summary>
    /// <param name="original">The raw downloaded image.</param>
    /// <returns>The processed image, or <paramref name="original"/> unchanged if processing wasn't possible.</returns>
    public static async Task<CoverArtResult> ProcessAsync(CoverArtResult original)
    {
        ArgumentNullException.ThrowIfNull(original);

        var isLossless = LosslessExtensions.Contains(original.FileExtension);
        var outputExtension = isLossless ? ".jpg" : original.FileExtension;

        var baseName = Path.GetRandomFileName();
        var inputPath = Path.Combine(Path.GetTempPath(), baseName + ".in" + original.FileExtension);
        var outputPath = Path.Combine(Path.GetTempPath(), baseName + ".out" + outputExtension);

        try
        {
            await File.WriteAllBytesAsync(inputPath, original.Content).ConfigureAwait(false);

            var startInfo = BuildStartInfo(inputPath, outputPath, isLossless);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return original;
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                return original;
            }

            var processedBytes = await File.ReadAllBytesAsync(outputPath).ConfigureAwait(false);
            return new CoverArtResult(processedBytes, outputExtension);
        }
        catch (Exception ex) when (ex is Win32Exception or IOException)
        {
            return original;
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    /// <summary>Builds the ImageMagick invocation for a resize (and optional lossless→JPEG conversion).</summary>
    /// <param name="inputPath">The source image path.</param>
    /// <param name="outputPath">The destination image path.</param>
    /// <param name="isLossless">Whether to also apply JPEG re-encoding at <see cref="JpegQuality"/>.</param>
    /// <returns>The configured start info, not yet started.</returns>
    internal static ProcessStartInfo BuildStartInfo(string inputPath, string outputPath, bool isLossless)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "magick",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("-resize");
        startInfo.ArgumentList.Add(MaxDimensions);
        if (isLossless)
        {
            startInfo.ArgumentList.Add("-quality");
            startInfo.ArgumentList.Add(JpegQuality);
        }

        startInfo.ArgumentList.Add(outputPath);
        return startInfo;
    }

    /// <summary>Deletes a file if it exists, ignoring any I/O failure.</summary>
    /// <param name="path">The file to delete.</param>
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
