namespace Whatinator.Core.Toc;

/// <summary>
/// The disc TOC read operation <see cref="Drive.OffsetFinder"/> depends on --
/// exists purely so it can be unit-tested against a fake implementation
/// instead of the real, process-spawning <see cref="CdrdaoTocReader"/>. Every
/// other caller of <see cref="CdrdaoTocReader"/> (<c>RipCommand</c>/
/// <c>PipelineRunner</c>/<c>TocCommand</c>) still uses the concrete class
/// directly -- same single-purpose-test-seam intent as
/// <see cref="Rip.ICdParanoiaTrackReader"/>/<see cref="Flac.IFlacEncoder"/>.
/// </summary>
public interface ICdrdaoTocReader
{
    /// <summary>Runs <c>cdrdao read-toc</c> against <paramref name="device"/> and parses the resulting <c>.toc</c> file.</summary>
    /// <param name="device">The block device to read, e.g. <c>/dev/sr1</c>.</param>
    /// <param name="fastToc">When <see langword="true"/>, passes <c>--fast-toc</c> (track start/length only, no pregap scan).</param>
    /// <param name="standardOutput">The stream to relay cdrdao's live progress into.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The parsed, frame-accurate table of contents.</returns>
    Task<DiscToc> ReadAsync(
        string device,
        bool fastToc,
        Stream standardOutput,
        CancellationToken cancellationToken = default);
}
