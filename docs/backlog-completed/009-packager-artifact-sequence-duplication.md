# FlacPackager and Mp3Packager duplicate the whole container-artifact sequence

**Status:** done

## Description

Larger than the two duplications already flagged, combined.

| Concern | FlacPackager | Mp3Packager | Difference |
| --- | --- | --- | --- |
| container/disc dir resolution | `:53-58` | `:57-64` | naming class only |
| `WriteChecksums` | `:123-128` | `:199-204` | `"*.flac"` vs `"*.mp3"` |
| `WritePlaylist` | `:142-169` | `:216-243` | `"*.flac"` vs `"*.mp3"` |
| `ToRelativePath` | `:199-200` | `:249-250` | **none -- identical** |
| artifact sequence | `:70-73` | `:88-91` | **none -- identical 4 lines** |

The artifact sequence is literally the same four calls in the same order:

```csharp
ReleaseInfoFile.Save(releaseInfo, Path.Combine(containerDir, "releaseinfo.json"));
IdTextFile.Write(releaseInfo, Path.Combine(containerDir, "id.txt"));
WriteChecksums(containerDir);
WritePlaylist(releaseInfo, containerDir, isMultiDisc);
```

`ToRelativePath` -- an identical one-liner -- exists in **four** places:
`FlacPackager.cs:199`, `Mp3Packager.cs:249`, `MetadataUpdater.cs:135`,
`ChecksumFile.cs:142`.

There is also a `WriteLineAsync`-to-a-raw-`Stream` helper duplicated four times:
`OffsetFinder.cs:218-223`, `CdrdaoTocReader.cs:136-141`,
`WhatinatorRipRunner.cs:188-193`, `CdParanoiaTrackReader.cs:465-470` (the last two
adding `RipOutputTimestamp.Prefix()`), plus an inline copy at `Mp3Packager.cs:126-128`.

## Why it matters beyond tidiness

The idempotent-rescan behaviour is a core invariant of this project (packaging
must be safe to run once per disc of a multi-disc release, in any order, across
separate sessions). Having it implemented twice means it can drift into being
true for FLAC and false for MP3 without anything catching it.

More concretely, the audio-only manifest defect lives in **three** copies of
`WriteChecksums` -- consolidating fixes it in one place instead of three.

## Proposed fix

A `ReleasePackageArtifacts.Write(ReleaseInfo releaseInfo, string containerDir, bool isMultiDisc, string audioExtension)`
in `Whatinator.Core`, owning all four steps plus the two rescans. That also
removes the doc-only `Checksums -> Flac/Mp3` `using` cycle at `ChecksumFile.cs:2-3`.

Then one internal `ToRelativePath` helper (`ChecksumFile` is the natural home),
and one `StreamLineWriter` with an optional timestamp flag.

## Acceptance Criteria

- [ ] `ReleasePackageArtifacts` (or similar) owns the artifact sequence,
      `WriteChecksums`, and `WritePlaylist`, parameterised by audio extension.
- [ ] Both packagers call it; their private copies deleted.
- [ ] Single `ToRelativePath`; the four copies deleted.
- [ ] Single line-writer helper; the five copies deleted.
- [ ] Existing `FlacPackagerTests` and `Mp3PackagerTests` still pass unchanged --
      this is a pure refactor with no behavioural change, so no test should need
      editing. Any test that does need editing indicates a behaviour change to
      investigate.
- [ ] New test on the shared writer directly: idempotency (call twice, identical
      result) and the degraded-disc case (fewer files than tracks still
      contributes to the `.m3u`).
- [ ] Verify packaged output is byte-identical to before, for both a single-disc
      and a multi-disc release.
