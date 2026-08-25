using System.Security.Cryptography;

namespace Whatinator.Core.Checksums;

/// <summary>Writes <c>checksum_sha256.txt</c> manifests: one SHA-256 hash + relative path per line.</summary>
public static class ChecksumFile
{
    /// <summary>The manifest's standard filename, written into and read from a folder's root.</summary>
    private const string ManifestFileName = "checksum_sha256.txt";

    /// <summary>Computes SHA-256 for each given file and writes a manifest, sorted by relative path.</summary>
    /// <param name="files">Each file's relative (for display) and absolute (for reading) path.</param>
    /// <param name="checksumFilePath">The destination manifest file path.</param>
    public static void Write(IEnumerable<(string RelativePath, string AbsolutePath)> files, string checksumFilePath)
    {
        ArgumentNullException.ThrowIfNull(files);

        var lines = files
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .Select(file => $"{ComputeSha256(file.AbsolutePath)} {file.RelativePath}");

        File.WriteAllLines(checksumFilePath, lines);
    }

    /// <summary>
    /// Recursively hashes every file under <paramref name="directory"/>
    /// (except the manifest itself) and writes
    /// <c>checksum_sha256.txt</c> at its root. Unlike
    /// <see cref="Whatinator.Core.ReleasePackageArtifacts.WriteChecksums"/>,
    /// which only hashes the one audio extension it knows a release folder
    /// holds, this makes no assumption about what kind of folder it's given
    /// -- used by the standalone <c>make-checksum</c> command.
    /// </summary>
    /// <param name="directory">The folder to checksum.</param>
    /// <returns>How many files were hashed.</returns>
    public static int Generate(string directory)
    {
        var manifestPath = Path.Combine(directory, ManifestFileName);
        var files = EnumerateFiles(directory)
            .Where(path => !string.Equals(path, manifestPath, StringComparison.Ordinal))
            .Select(path => (RelativePath: ToRelativePath(directory, path), AbsolutePath: path))
            .ToList();

        Write(files, manifestPath);
        return files.Count;
    }

    /// <summary>
    /// Reads <c>checksum_sha256.txt</c> at the root of
    /// <paramref name="directory"/> and compares it against what's
    /// actually on disk, recursively.
    /// </summary>
    /// <remarks>
    /// Each manifest entry's resolved path is checked against
    /// <paramref name="directory"/> before it is read or hashed -- an entry
    /// containing <c>..</c> traversal, or an absolute path, is reported via
    /// <see cref="ChecksumCompareResult.Malformed"/> instead of being
    /// followed. Without this, a manifest crafted (or corrupted) to point
    /// outside the target folder would turn <c>compare-checksum</c> into a
    /// file-disclosure oracle over the rest of the filesystem -- see
    /// <c>docs/backlog-completed/024-checksum-compare-path-traversal.md</c>.
    /// </remarks>
    /// <param name="directory">The folder to verify.</param>
    /// <returns>The categorized comparison result.</returns>
    /// <exception cref="FileNotFoundException"><paramref name="directory"/> has no <c>checksum_sha256.txt</c>.</exception>
    public static ChecksumCompareResult Compare(string directory)
    {
        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"No {ManifestFileName} found in '{directory}'.", manifestPath);
        }

        var listed = ParseManifest(manifestPath);
        var resolvedDirectory = Path.GetFullPath(directory);
        var allowedPrefix = resolvedDirectory + Path.DirectorySeparatorChar;

        var matched = new List<string>();
        var mismatched = new List<ChecksumMismatch>();
        var missing = new List<string>();
        var malformed = new List<string>();

        foreach (var (relativePath, expected) in listed)
        {
            var absolutePath = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var resolvedPath = Path.GetFullPath(absolutePath);
            if (!resolvedPath.StartsWith(allowedPrefix, StringComparison.Ordinal))
            {
                malformed.Add(relativePath);
                continue;
            }

            if (!File.Exists(resolvedPath))
            {
                missing.Add(relativePath);
                continue;
            }

            var actual = ComputeSha256(resolvedPath);
            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                matched.Add(relativePath);
            }
            else
            {
                mismatched.Add(new ChecksumMismatch(relativePath, expected, actual));
            }
        }

        var listedPaths = listed.Select(entry => entry.RelativePath).ToHashSet(StringComparer.Ordinal);
        var extra = EnumerateFiles(directory)
            .Where(path => !string.Equals(path, manifestPath, StringComparison.Ordinal))
            .Select(path => ToRelativePath(directory, path))
            .Where(relativePath => !listedPaths.Contains(relativePath))
            .OrderBy(relativePath => relativePath, StringComparer.Ordinal)
            .ToList();

        return new ChecksumCompareResult(matched, mismatched, missing, extra, malformed);
    }

    /// <summary>
    /// Converts an absolute path to a forward-slash-separated path relative
    /// to <paramref name="baseDir"/>. The single implementation shared by
    /// every class that writes a relative-path manifest entry (checksum
    /// lines, <c>.m3u</c> entries) -- see <see cref="ReleasePackageArtifacts"/>
    /// and <see cref="Whatinator.Core.Metadata.MetadataUpdater"/>.
    /// </summary>
    /// <param name="baseDir">The base directory.</param>
    /// <param name="fullPath">The absolute path to make relative.</param>
    /// <returns>The relative path, using <c>/</c> regardless of host OS.</returns>
    internal static string ToRelativePath(string baseDir, string fullPath) =>
        Path.GetRelativePath(baseDir, fullPath).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>Parses a <c>checksum_sha256.txt</c> manifest's lines into (relative path, hash) pairs.</summary>
    /// <param name="manifestPath">The manifest file to read.</param>
    /// <returns>Each line's relative path and expected hash.</returns>
    private static List<(string RelativePath, string Hash)> ParseManifest(string manifestPath)
    {
        var entries = new List<(string, string)>();
        foreach (var line in File.ReadAllLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(' ', StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                continue;
            }

            var hash = line[..separatorIndex];
            var pathStart = separatorIndex + 1;
            var relativePath = line[pathStart..];
            entries.Add((relativePath, hash));
        }

        return entries;
    }

    /// <summary>Enumerates every regular file under <paramref name="directory"/>, recursively.</summary>
    /// <param name="directory">The directory to scan.</param>
    /// <returns>Absolute file paths.</returns>
    private static IEnumerable<string> EnumerateFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);

    /// <summary>Computes the uppercase-hex SHA-256 digest of a file's contents.</summary>
    /// <param name="path">The file to hash.</param>
    /// <returns>The uppercase hex digest.</returns>
    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
