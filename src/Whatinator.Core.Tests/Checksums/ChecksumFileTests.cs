using System.Security.Cryptography;
using Whatinator.Core.Checksums;

namespace Whatinator.Core.Tests;

public class ChecksumFileTests : IDisposable
{
    private readonly string _tempDir;

    public ChecksumFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "whatinator-checksum-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Write_ProducesCorrectHashAndFormat()
    {
        var filePath = Path.Combine(_tempDir, "01. Track.flac");
        File.WriteAllText(filePath, "fake flac content");
        var expectedHash = Convert.ToHexString(SHA256.HashData("fake flac content"u8.ToArray()));
        var checksumPath = Path.Combine(_tempDir, "checksum_sha256.txt");

        ChecksumFile.Write([("01. Track.flac", filePath)], checksumPath);

        var line = Assert.Single(File.ReadAllLines(checksumPath));
        Assert.Equal($"{expectedHash} 01. Track.flac", line);
    }

    [Fact]
    public void Write_SortsByRelativePath()
    {
        var pathB = Path.Combine(_tempDir, "b.flac");
        var pathA = Path.Combine(_tempDir, "a.flac");
        File.WriteAllText(pathB, "b");
        File.WriteAllText(pathA, "a");

        var checksumPath = Path.Combine(_tempDir, "checksum_sha256.txt");
        ChecksumFile.Write([("b.flac", pathB), ("a.flac", pathA)], checksumPath);

        var lines = File.ReadAllLines(checksumPath);
        Assert.EndsWith("a.flac", lines[0]);
        Assert.EndsWith("b.flac", lines[1]);
    }

    [Fact]
    public void Generate_HashesEveryFileRecursivelyExceptItsOwnManifest()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "a");
        var subDir = Path.Combine(_tempDir, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "b.txt"), "b");

        var count = ChecksumFile.Generate(_tempDir);

        Assert.Equal(2, count);
        var lines = File.ReadAllLines(Path.Combine(_tempDir, "checksum_sha256.txt"));
        Assert.Equal(2, lines.Length);
        Assert.Contains(lines, line => line.EndsWith("a.txt", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.EndsWith("sub/b.txt", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_IsIdempotent_ManifestDoesNotHashItself()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "a");

        ChecksumFile.Generate(_tempDir);
        var secondCount = ChecksumFile.Generate(_tempDir);

        Assert.Equal(1, secondCount);
    }

    [Fact]
    public void Compare_AllMatch_ReturnsClean()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "a");
        ChecksumFile.Generate(_tempDir);

        var result = ChecksumFile.Compare(_tempDir);

        Assert.True(result.IsClean);
        Assert.Equal(["a.txt"], result.Matched);
        Assert.Empty(result.Mismatched);
        Assert.Empty(result.Missing);
        Assert.Empty(result.Extra);
    }

    [Fact]
    public void Compare_TamperedFile_ReportsMismatch()
    {
        var path = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(path, "a");
        ChecksumFile.Generate(_tempDir);
        File.WriteAllText(path, "tampered");

        var result = ChecksumFile.Compare(_tempDir);

        Assert.False(result.IsClean);
        Assert.Empty(result.Matched);
        var mismatch = Assert.Single(result.Mismatched);
        Assert.Equal("a.txt", mismatch.RelativePath);
        Assert.NotEqual(mismatch.Expected, mismatch.Actual);
    }

    [Fact]
    public void Compare_DeletedFile_ReportsMissing()
    {
        var path = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(path, "a");
        ChecksumFile.Generate(_tempDir);
        File.Delete(path);

        var result = ChecksumFile.Compare(_tempDir);

        Assert.False(result.IsClean);
        Assert.Equal(["a.txt"], result.Missing);
    }

    [Fact]
    public void Compare_UnlistedFile_ReportsExtraButStaysClean()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "a");
        ChecksumFile.Generate(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "b.txt"), "b");

        var result = ChecksumFile.Compare(_tempDir);

        Assert.True(result.IsClean);
        Assert.Equal(["b.txt"], result.Extra);
    }

    [Fact]
    public void Compare_NoManifest_Throws() =>
        Assert.Throws<FileNotFoundException>(() => ChecksumFile.Compare(_tempDir));

    [Fact]
    public void Compare_TraversalEntry_ReportsMalformedWithoutReadingOutsideFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "a");
        ChecksumFile.Generate(_tempDir);

        var outsideDir = Path.Combine(Path.GetTempPath(), "whatinator-checksum-outside-" + Guid.NewGuid());
        Directory.CreateDirectory(outsideDir);
        try
        {
            var outsidePath = Path.Combine(outsideDir, "secret.txt");
            File.WriteAllText(outsidePath, "secret");

            var manifestPath = Path.Combine(_tempDir, "checksum_sha256.txt");
            var hash = Convert.ToHexString(SHA256.HashData("secret"u8.ToArray()));
            File.AppendAllLines(manifestPath, [$"{hash} ../{Path.GetFileName(outsideDir)}/secret.txt"]);

            var result = ChecksumFile.Compare(_tempDir);

            Assert.False(result.IsClean);
            Assert.Equal(["a.txt"], result.Matched);
            Assert.Empty(result.Mismatched);
            Assert.Empty(result.Missing);
            Assert.Single(result.Malformed);
            Assert.Contains("secret.txt", result.Malformed[0]);
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }

    [Fact]
    public void Compare_AbsolutePathEntry_ReportsMalformed()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "a");
        ChecksumFile.Generate(_tempDir);

        var manifestPath = Path.Combine(_tempDir, "checksum_sha256.txt");
        var absolutePath = OperatingSystem.IsWindows() ? "C:/Windows/System32/drivers/etc/hosts" : "/etc/passwd";
        File.AppendAllLines(manifestPath, [$"0000000000000000000000000000000000000000000000000000000000000000 {absolutePath}"]);

        var result = ChecksumFile.Compare(_tempDir);

        Assert.False(result.IsClean);
        Assert.Single(result.Malformed);
        Assert.Equal(absolutePath, result.Malformed[0]);
    }
}
