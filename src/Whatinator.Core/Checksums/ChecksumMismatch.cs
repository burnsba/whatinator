namespace Whatinator.Core.Checksums;

/// <summary>One file whose current SHA-256 doesn't match its <c>checksum_sha256.txt</c> entry.</summary>
/// <param name="RelativePath">The file's path, relative to the checksummed folder.</param>
/// <param name="Expected">The hash recorded in the manifest.</param>
/// <param name="Actual">The file's current hash.</param>
public sealed record ChecksumMismatch(string RelativePath, string Expected, string Actual);
