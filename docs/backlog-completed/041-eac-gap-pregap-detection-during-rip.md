# EAC gap: rips use --fast-toc, so pregaps are never detected

**Status:** done

## Description

**EAC feature:** pre-track gap detection (`gap-technology.txt`) -- EAC's
"Detect Pre-Track Gaps" reads sub-channel information to find where each track
actually ends.

**Status in whatinator: PARTIAL.** The capability exists but the rip path does
not use it.

`TocFileParser` handles `PREGAP`/`START` and populates
`DiscTocTrack.PregapFrames`, and `toc --full` prints it
(`Whatinator.Cli/TocFormatter.cs`). But both rip paths request a fast TOC:

- `src/Whatinator.Cli/RipCommand.cs:91` -- `ReadAsync(device, fastToc: true, standardError)`
- `src/Whatinator.Core/Rip/PipelineRunner.cs:69` -- `ReadAsync(options.Device, fastToc: true, standardError, cancellationToken)`

Per `DiscTocTrack`'s own doc comment, fast mode only ever reports **track 1's**
pregap (read from the raw TOC, not audio-scanned). Every other track's pregap
requires the slow scan `--fast-toc` skips.

## Consequence

`WhatinatorEacLog`'s "Pre-gap length" line is effectively track-1-only in every
real rip, and any future cue sheet would be too. The information is silently
absent rather than reported as unavailable.

Note the related ambiguity documented on `DiscTocTrack.PregapFrames`: `null`
means both "not scanned" and "scanned, found zero", so a consumer cannot tell
the difference. That is worth resolving as part of this work.

## Trade-off

A full scan costs real drive time -- cdrdao must scan audio content across every
track boundary, roughly a second per track and often more. That is why fast mode
was chosen. So this should be **opt-in**, not the new default.

Effort: low. The parser, the formatter, and the reader parameter all already
exist; this is a flag and some threading.

## Acceptance Criteria

- [ ] A `--full-toc` (or similar) flag added to `rip` and `pipeline`, threading
      `fastToc: false` through to `CdrdaoTocReader.ReadAsync`.
- [ ] Default remains fast, with the cost of the full scan stated in `--help` and
      the README.
- [ ] `DiscTocTrack.PregapFrames` distinguishes "not scanned" from "scanned,
      found zero" -- e.g. a separate `PregapScanned` flag, or a sentinel -- so the
      log can print "not detected" rather than silently omitting.
- [ ] The rip log's "Pre-gap length" line reflects real per-track values when a
      full scan was performed.
- [ ] New tests: a full-mode `.toc` fixture produces per-track pregaps; a
      fast-mode fixture produces only track 1's and marks the rest unscanned.
- [ ] Manual verification against a real disc with known 2-second gaps.
