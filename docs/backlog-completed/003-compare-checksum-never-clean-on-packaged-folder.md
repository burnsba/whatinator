# compare-checksum can never report clean on a packaged folder

**Status:** done

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

## Design decision

Neither of the two options originally sketched here (manifest-covers-everything
vs. manifest-stays-audio-only-with-Extra-informational) landed cleanly, since
each packaged format has one non-audio file that's worth checksumming (the rip
log) and one that will be soon (the cue sheet, FLAC only -- see backlog 040).
The decision actually taken is a per-format allowlist, combined with making
`Extra` informational:

- **FLAC folder manifest** tracks: every `.flac` file, the `.log` file, and
  (once cue generation ships -- see backlog 040) the `.cue` file. Does **not**
  track `cover.*`, `id.txt`, `releaseinfo.json`, or `.m3u`.
- **MP3 folder manifest** tracks: every `.mp3` file and the `.log` file. Does
  **not** track `cover.*`, `id.txt`, `releaseinfo.json`, or `.m3u`.
- Since those untracked files are always present in a packaged folder,
  `ChecksumCompareResult.IsClean` no longer requires `Extra.Count == 0` --
  only `Mismatched.Count == 0 && Missing.Count == 0`. `Extra` is still
  populated and reported by `compare-checksum`, it just no longer fails the
  exit code.
- `make-checksum`/`ChecksumFile.Generate` stays exactly what it already
  documented itself as: a deliberately format-agnostic "hash everything in
  this folder" tool, unrelated to what the packagers write. Running it over a
  packaged folder producing a different (everything) manifest is intentional,
  not a bug to fix here -- the two commands have different purposes, not
  mismatched semantics.

## Acceptance Criteria

- [x] Decision made and recorded (in source comments and README): per-format
      manifest allowlist (audio + log, + cue for FLAC once it exists), `Extra`
      made informational rather than unclean.
- [x] Logic changed so a freshly packaged folder passes `compare-checksum` with
      exit code 0.
- [x] `make-checksum` remains intentionally distinct from the packagers'
      manifest scope; documented as such rather than unified.
- [x] New test: full round trip -- package a disc, then call
      `ChecksumFile.Compare` on the result and assert `IsClean` is true. This is
      the missing seam test.
- [x] New test: `update-metadata` on a packaged folder, then `Compare`, still
      clean (`MetadataUpdater` has its own third copy of the manifest writer).
- [x] README's `compare-checksum` description updated for the new exit-code
      semantics.
