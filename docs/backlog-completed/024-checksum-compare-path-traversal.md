# ChecksumFile.Compare trusts relative paths from the manifest

**Status:** done

## Description

`src/Whatinator.Core/Checksums/ChecksumFile.cs:75`

```csharp
var absolutePath = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
```

`relativePath` comes straight from the manifest file with no validation. A
manifest containing `../../etc/passwd` makes `compare-checksum` read and hash
files outside the target folder, and report on them.

Severity is low: `checksum_sha256.txt` is a file whatinator wrote itself, in a
folder the user owns. But manifests travel with release folders, and a release
folder could come from somewhere else -- at which point `compare-checksum`
becomes a file-disclosure oracle over the user's filesystem.

The guard is two lines and there is no reason not to have it.

## Acceptance Criteria

- [ ] Each resolved path checked against the target directory:
      `Path.GetFullPath(absolutePath).StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.Ordinal)`.
- [ ] An entry that escapes the folder is reported as a malformed manifest entry
      (surfaced to the user), not silently skipped and not followed.
- [ ] Absolute paths in a manifest rejected the same way.
- [ ] New tests: a manifest entry containing `..` and one containing an absolute
      path are both rejected without reading the target file.
