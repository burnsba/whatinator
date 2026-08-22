# EAC gap: no whole-disc CRC in the rip log

**Status:** not started

## Description

**EAC feature:** EAC logs a per-disc CRC alongside the per-track Test/Copy CRCs.

**Status in whatinator: MISSING.**

whatinator has per-track CRC32s (`WhatinatorEacLog.AppendTracks` emits `Test CRC`
and `Copy CRC`) but nothing spanning the whole disc.

Two things that exist and are **not** this:

- `WhatinatorEacLog.Format`'s `==== Log checksum ====` footer -- a SHA-256 over
  the log text itself, verifying the log has not been edited.
- `checksum_sha256.txt` -- a per-file manifest of the packaged output.

Neither is a checksum of the disc's audio as a whole.

## Related observation on the per-track CRCs

`AppendTracks` prints the **same** `trackResult.Crc32` for both `Test CRC` and
`Copy CRC`. That is legitimate -- the track is only accepted when the two reads'
CRC32s matched, so they are equal by construction -- but they are not two
independently-reported values as in EAC, and a reader comparing logs may draw the
wrong conclusion about what was verified. Worth a clarifying comment in the log
or the source either way.

## Value assessment

Modest. AccurateRip verification is a far stronger check than a self-computed
disc CRC, and whatinator already has it. The main value is log-format parity for
anyone comparing whatinator logs against EAC logs.

## Acceptance Criteria

- [ ] Decision recorded on whether disc-level CRC is worth adding at all, given
      AccurateRip coverage -- closing this as a non-goal is a legitimate outcome.

If adding it:

- [ ] A whole-disc CRC computed over the concatenated accepted track audio, in
      track order, and emitted in the conclusive summary block.
- [ ] The algorithm and byte-range convention documented precisely, so the value
      is reproducible -- an undocumented checksum is worse than none.
- [ ] Degraded rips (missing tracks) either omit it or mark it as partial; they
      must not emit a value that looks comparable to a complete rip's.
- [ ] The Test CRC / Copy CRC equality noted in a comment so the identical values
      are not mistaken for a bug.
- [ ] New tests pinning the disc CRC for a known set of track inputs.
