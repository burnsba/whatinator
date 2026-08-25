namespace Whatinator.Core.Checksums;

/// <summary>The outcome of <see cref="ChecksumFile.Compare"/>, categorizing every listed and every found file.</summary>
/// <param name="Matched">Files whose current hash matches the manifest.</param>
/// <param name="Mismatched">Files whose current hash differs from the manifest.</param>
/// <param name="Missing">Files listed in the manifest but absent on disk.</param>
/// <param name="Extra">
/// Files present on disk (recursively) but not listed in the manifest. A
/// packaged release folder always has some of these -- <c>id.txt</c>,
/// <c>releaseinfo.json</c>, cover art, <c>.m3u</c> -- by design (see
/// <see cref="Whatinator.Core.Flac.FlacPackager"/>/<see cref="Whatinator.Core.Mp3.Mp3Packager"/>'s
/// manifest scope, root <c>CLAUDE.md</c> § Gotchas is silent on this but
/// <c>docs/backlog-completed/003-compare-checksum-never-clean-on-packaged-folder.md</c>
/// records the decision). <see cref="Extra"/> is reported for visibility but
/// does not affect <see cref="IsClean"/>.
/// </param>
/// <param name="Malformed">
/// Manifest entries rejected because their resolved path escapes the target
/// directory -- an absolute path, or a relative path containing <c>..</c>
/// traversal. These are neither read nor hashed; see
/// <see cref="Whatinator.Core.Checksums.ChecksumFile.Compare"/> and
/// <c>docs/backlog-completed/024-checksum-compare-path-traversal.md</c>.
/// </param>
public sealed record ChecksumCompareResult(
    IReadOnlyList<string> Matched,
    IReadOnlyList<ChecksumMismatch> Mismatched,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Extra,
    IReadOnlyList<string> Malformed)
{
    /// <summary>
    /// Whether every listed file matched with nothing missing and no manifest
    /// entry was rejected as malformed. Deliberately ignores <see cref="Extra"/>
    /// -- a packaged release folder always has unlisted files by design, so
    /// requiring zero of them would make a packaged folder unable to ever
    /// report clean. <see cref="Malformed"/>, by contrast, always counts
    /// against cleanliness -- a manifest containing a path-escaping entry is
    /// untrustworthy regardless of what else matched.
    /// </summary>
    public bool IsClean => Mismatched.Count == 0 && Missing.Count == 0 && Malformed.Count == 0;
}
