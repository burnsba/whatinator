# EAC gap: CUE sheet generation

**Status:** done

## Description

**EAC feature:** "Automatic creation of CUE sheets for Burnnn, Feurio, Nero or
even EAC, which can include all gaps, indicies, track attributes, UPC and ISRC
and also CD-Text for an exact copy" (`features-of-eac.txt`).

**Status in whatinator: MISSING.** Nothing generates a cue sheet.
`grep -rn -i cue src/` yields only `WhatinatorEacLog`'s "never CUETools" comment.

`example/Glorious.cue` is an **EAC-produced reference file** kept on hand as a
format target -- its header reads `REM COMMENT "ExactAudioCopy v1.5"` -- not
project output.

## Why this is the highest-value gap

Without a cue sheet a rip cannot be losslessly reconstructed to disc, which is a
core part of what "exact copy" means. And the data a cue sheet carries -- gaps,
indices, UPC, ISRC -- is data whatinator **already parses** and currently has
nowhere to put (see the ISRC/UPC and pregap backlog items). The cue sheet is the
artifact that gives all of it a home.

## Status update (backlog 042)

`DiscToc.CatalogNumber` (UPC) and per-track `DiscTocTrack.Isrc` are now threaded
through to the rip log, FLAC tags, and `id.txt` (see
`docs/backlog-completed/042-eac-gap-isrc-upc-discarded.md`) -- but that plumbing
is per-call (`EacLogOptions`/`FlacEncodeOptions`/`FlacPackageOptions`), not a new
persisted field on `ReleaseInfo`. A future cue-sheet writer should read
`DiscToc` directly, the same way `WhatinatorEacLog` and `WhatinatorRipRunner`
already do, rather than expecting UPC/ISRC to appear on `ReleaseInfo`.

## Status update (backlog 003)

`FlacPackager`'s checksum manifest now tracks `.flac` + `.log` files (see
`docs/backlog-completed/003-compare-checksum-never-clean-on-packaged-folder.md`).
When this item ships the `.cue` file, add `*.cue` to `FlacPackager`'s manifest
patterns (`WriteChecksums`/`EnumerateManifestFiles`) and to
`MetadataUpdater.WriteChecksums`'s pattern list, so the cue sheet is covered by
`checksum_sha256.txt` like every other artifact meant to be integrity-checked.
`Extra` is informational post-003, so skipping this wouldn't break
`compare-checksum`'s exit code -- but the cue would go unverified, which
defeats its purpose as part of an "exact copy."

## Dependencies

- A gaps-accurate cue sheet requires per-track pregaps, which are currently never
  scanned during a rip -- see the `--fast-toc` backlog item. Without that, a
  generated cue would carry track 1's pregap only.
- A truly index-accurate cue would need index detection beyond index 0/1, which
  cdrdao's full scan can supply but the parser does not currently model.

## Scope suggestion

Start with the achievable version: one `FILE` entry per track (matching this
project's file-per-track output), `TRACK ... AUDIO`, `INDEX 01`, plus `REM`
lines for the disc `CATALOG` (UPC) and per-track `ISRC` where known. Written at
the container level alongside `id.txt` / `.m3u` by the shared artifact writer.

Effort: roughly 2-4 hours for that; more for a genuinely index-accurate sheet.

## Acceptance Criteria

- [x] A `.cue` file is written into the FLAC release folder by the packager,
      regenerated idempotently by rescan like every other container artifact.
      Shipped as `CueSheetFile`, called from `FlacPackager.PackageAsync`.
      One caveat from the design phase: unlike the other container-level
      artifacts, the cue sheet is a genuinely *per-disc* artifact (its `FILE`
      lines name that disc's own audio files) and needs that call's own
      `DiscToc` -- a physical fact a directory rescan alone can't recover --
      so it's written once per `PackageAsync` call from
      `FlacPackageOptions.Toc`, not derived from rescanning the folder the
      way `id.txt`/`.m3u`/checksums are. See root `CLAUDE.md` § "Packaging is
      idempotent by rescan".
- [x] Contains: `CATALOG` (from `DiscToc.CatalogNumber`) when known, `PERFORMER`,
      `TITLE`, and per track: `FILE`, `TRACK nn AUDIO`, `TITLE`, `PERFORMER`,
      `ISRC` when known, `INDEX 01`.
- [x] Pregaps emitted as `INDEX 00` when a full TOC scan supplied them;
      omitted rather than guessed when it did not. Because a track's ripped
      audio includes the *following* track's pregap (root `CLAUDE.md` §
      Gotchas), a known pregap's `INDEX 00` is emitted at the tail of the
      *previous* track's `FILE` block, matching `example/Glorious.cue`'s own
      structure -- and track 1's own pregap (the only one `--fast-toc`
      reports, but never actually captured in any ripped file) is
      deliberately never rendered.
- [x] Decision recorded: no cue sheet for the MP3 folder -- see the doc
      comments on `Mp3Packager` and `CueSheetFile`.
- [x] New tests in `CueSheetFileTests.cs`: single-disc, multi-disc,
      various-artists, degraded rip (missing track), known/unscanned/absent
      pregap and TOC cases. Extended `FlacPackagerTests.cs` for the
      packager-level wiring and checksum-manifest inclusion.
- [x] Output structure (FILE/TRACK/INDEX nesting, pregap-in-previous-file
      placement) cross-checked by eye against `example/Glorious.cue`; the
      `REM GENRE`/`DATE`/`COMMENT` lines in that EAC-produced reference are
      not modeled (out of scope per the scope suggestion above).
- [x] README and `--help` updated (`flac` command entries).
