# compare-checksum can never report clean on a packaged folder

**Status:** not started

## Description

`Flac/FlacPackager.cs:123`, `Mp3/Mp3Packager.cs:199`, `Metadata/MetadataUpdater.cs:98`
vs `Checksums/ChecksumFile.cs:93-99`

The packagers write a manifest containing **only** the audio files:

```csharp
var files = Directory.EnumerateFiles(containerDir, "*.flac", SearchOption.AllDirectories)
```

This matches `init.md`'s previously stated spec ("The flac folder should only create
checksums for flac files"). `init.md` has since been removed.

But `ChecksumFile.Compare` enumerates the folder **recursively** and reports
everything not listed as `Extra`, and `ChecksumCompareResult.IsClean` requires
`Extra.Count == 0`:

```csharp
public bool IsClean => Mismatched.Count == 0 && Missing.Count == 0 && Extra.Count == 0;
```

`compare-checksum`'s exit code comes straight from `IsClean`.

## Failure scenario

Run `pipeline`, producing a healthy folder. Then:

```
whatinator compare-checksum --dest "Artist - Album [flac 2001]"
```

It reports `Extra (not in manifest): 5` -- `id.txt`, `releaseinfo.json`,
`cover.jpg`, `*.m3u`, `*.log` -- and **exits 1**, on a folder that is completely
healthy. The README documents "Exits 0 if clean, 1 otherwise"; for a packaged
release folder the clean path is unreachable.

Compounding the confusion: `make-checksum` on the same folder calls
`ChecksumFile.Generate`, which hashes **everything** (excluding only the manifest
itself). So the two commands disagree about what a manifest is supposed to
contain, and running `make-checksum` over a packaged folder silently replaces the
packager's audio-only manifest with an everything manifest.

## Why the tests miss it

`ChecksumFileTests` exercises `Generate`/`Compare` in isolation, and
`FlacPackagerTests`/`Mp3PackagerTests` exercise packaging in isolation. Nothing
tests the seam between them, which is exactly where this lives.

## Design decision required

Pick one, and record it:

1. **Manifest covers everything** -- have the packagers call
   `ChecksumFile.Generate(containerDir)` (which already exists and already
   excludes the manifest itself) and delete the three private `WriteChecksums`
   copies. Contradicts `init.md`, but makes `compare-checksum` meaningful as an
   archival integrity check over the whole release.
   Ordering constraint: `FlacPackager.PackageAsync:70-75` writes checksums
   **before** fetching cover art, so the cover-art step must move above the
   checksum step or the cover would be perpetually "extra".
2. **Manifest stays audio-only** -- change `IsClean` so `Extra` is informational
   rather than unclean, and make `make-checksum` consistent with that.

Option 1 is the stronger archival guarantee and folds cleanly into the shared
artifact writer proposed in the packager-duplication backlog item.

## Acceptance Criteria

- [ ] Decision made and recorded (in source comment and README), on whether the
      manifest covers all files or audio only.
- [ ] Logic changed so a freshly packaged folder passes `compare-checksum` with
      exit code 0.
- [ ] `make-checksum` and the packagers produce manifests with the same semantics.
- [ ] If option 1: cover-art fetch moved above the checksum write in
      `FlacPackager.PackageAsync`.
- [ ] New test: full round trip -- package a disc, then call
      `ChecksumFile.Compare` on the result and assert `IsClean` is true. This is
      the missing seam test.
- [ ] New test: `update-metadata` on a packaged folder, then `Compare`, still
      clean (`MetadataUpdater` has its own third copy of the manifest writer).
- [ ] README's `compare-checksum` description updated if semantics change.
