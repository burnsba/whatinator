# EAC gap: track quality percentage is computed but never logged

**Status:** not started

## Description

**EAC feature:** per-track quality percentage in the rip log
(`extraction-technology.txt` -- EAC reports extraction confidence per track).

**Status in whatinator: computed, then discarded.**

`CdParanoiaTrackReader.ComputeQuality` produces the value,
`CdParanoiaTrackResult.Quality` carries it, and
`WhatinatorTrackRipResult.Quality` carries it further -- but
`WhatinatorEacLog.AppendTracks` never prints it. The per-track block emits
Filename, Pre-gap length, Peak level, Extraction speed, Test CRC, Copy CRC,
AccurateRip result, and Copy OK. No quality line.

Adding the line is a handful of characters.

## Blocking dependency -- do not do this first

The computed value is **currently wrong**. `ComputeQuality` treats cd-paranoia's
absolute disc offsets as track-relative, so quality saturates at 1.0 for every
track except track 1. See the `ComputeQuality` backlog item.

Surfacing the value before fixing the arithmetic would put a
uniformly-100%-looking field into the archival log, which is worse than having
no field: it would look like a verified quality claim.

**Order: fix the frame arithmetic, verify against a real rip, then add the log
line.**

## Acceptance Criteria

- [ ] The `ComputeQuality` frame-offset bug fixed and verified first.
- [ ] A quality line added to `WhatinatorEacLog.AppendTracks`, formatted to match
      EAC's convention (percentage, aligned with the surrounding fields).
- [ ] Rendered as "not available" rather than `100.0 %` when `Quality` is `null`
      (no parseable progress lines were captured).
- [ ] The conclusive summary block reflects quality where EAC's does.
- [ ] New tests in `WhatinatorEacLogTests` pinning the formatted line for a known
      quality value and for `null`.
- [ ] Manual verification against a real rip: values vary per track and are not
      uniformly 100%.
