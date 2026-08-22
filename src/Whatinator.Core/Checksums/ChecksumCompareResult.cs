namespace Whatinator.Core.Checksums;

/// <summary>The outcome of <see cref="ChecksumFile.Compare"/>, categorizing every listed and every found file.</summary>
/// <param name="Matched">Files whose current hash matches the manifest.</param>
/// <param name="Mismatched">Files whose current hash differs from the manifest.</param>
/// <param name="Missing">Files listed in the manifest but absent on disk.</param>
/// <param name="Extra">Files present on disk (recursively) but not listed in the manifest.</param>
public sealed record ChecksumCompareResult(
    IReadOnlyList<string> Matched,
    IReadOnlyList<ChecksumMismatch> Mismatched,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Extra)
{
    /// <summary>Whether every listed file matched, with nothing missing or unlisted.</summary>
    public bool IsClean => Mismatched.Count == 0 && Missing.Count == 0 && Extra.Count == 0;
}
