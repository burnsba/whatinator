# EAC gap: CUE sheet generation

**Status:** not started

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

- [ ] A `.cue` file is written into the FLAC release folder by the packager,
      regenerated idempotently by rescan like every other container artifact.
- [ ] Contains: `CATALOG` (from `DiscToc.CatalogNumber`) when known, `PERFORMER`,
      `TITLE`, and per track: `FILE`, `TRACK nn AUDIO`, `TITLE`, `PERFORMER`,
      `ISRC` when known, `INDEX 01`.
- [ ] Pregaps emitted as `INDEX 00` / `PREGAP` when a full TOC scan supplied them;
      omitted rather than guessed when it did not.
- [ ] Decision recorded on whether the MP3 folder also gets one (probably not --
      a cue over lossy files is not useful for reconstruction).
- [ ] New tests: cue output for a single-disc release, a multi-disc release, a
      various-artists release, and a degraded rip (missing tracks) -- compared
      against expected text.
- [ ] Output cross-checked by eye against `example/Glorious.cue` for structural
      conformance.
- [ ] README and `--help` updated if a new command or flag is introduced.
