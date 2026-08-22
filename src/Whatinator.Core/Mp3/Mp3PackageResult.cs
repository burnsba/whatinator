namespace Whatinator.Core.Mp3;

/// <summary>The outcome of a successful <see cref="Mp3Packager.PackageAsync"/> call.</summary>
/// <param name="ContainerDirectory">The release's MP3 container folder (holds <c>cd1/</c>/<c>cd2/</c> for multi-disc releases, or the MP3 files directly for single-disc).</param>
/// <param name="DiscDirectory">The directory this disc's MP3 files and log were written into (same as <paramref name="ContainerDirectory"/> for single-disc releases).</param>
/// <param name="EncodedTrackCount">How many tracks were encoded from <see cref="Mp3PackageOptions.SourceDirectory"/>.</param>
/// <param name="LogFilePath">Where this run's MP3 log was written.</param>
/// <param name="CoverArtPath">Where cover art was copied to (from the FLAC folder), or <see langword="null"/> if the FLAC folder had none.</param>
public sealed record Mp3PackageResult(
    string ContainerDirectory,
    string DiscDirectory,
    int EncodedTrackCount,
    string LogFilePath,
    string? CoverArtPath);
