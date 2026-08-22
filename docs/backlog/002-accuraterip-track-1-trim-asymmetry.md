# AccurateRip track-1 trim is asymmetric by one sample

**Status:** not started

## Description

`src/Whatinator.Core/AccurateRip/AccurateRipChecksum.cs:37`

```csharp
// 1-based inclusive bounds on the running MulBy position counter.
var from = 0;
var to = sampleCount;
if (trackNumber == 1)           { from += TrimSamplePairs; }   // 2940
if (trackNumber == totalTracks) { to   -= TrimSamplePairs; }
...
var mulBy = i + 1;
if (mulBy >= from && mulBy <= to) { ... }
```

`TrimSamplePairs` is `(2352 * 5) / 4` = 2940, i.e. 5 sectors' worth of L+R
sample pairs. Both ends are meant to trim the same 5 sectors. They do not:

- **Leading trim (track 1):** `from` = 2940, condition `mulBy >= from` with
  `mulBy = i + 1`, so samples with `i >= 2939` are included. Excluded:
  `i` in `[0, 2938]` -- **2939 samples**.
- **Trailing trim (last track):** `to` = N - 2940, condition `mulBy <= to`, so
  samples with `i <= N - 2941` are included. Excluded: `i` in
  `[N - 2940, N - 1]` -- **2940 samples**.

The comment on the line above already declares these are "1-based inclusive
bounds", which requires `var from = 1;`. As written, sample index 2939 (the last
sample of track 1's 5th sector) is folded into the checksum with multiplier 2940.

The widely-used public reference implementations skip `i < 5 * 588` for track 1
and `i >= samples - 5 * 588` for the last track -- symmetric, 2940 each.

## Why this matters

If the leading end is the wrong one, **track 1 of every disc computes a v1/v2
that no AccurateRip submission will ever equal**:

- `WhatinatorRipRunner.RipAsync` -> `AccurateRipClient.MatchAsync` reports track 1
  as unverifiable on every rip, and the EAC log summary degrades to
  "Some tracks could not be verified" even on a perfect rip.
- Worse, `Drive/OffsetFinder.cs:131-137` gates the **entire** offset search on
  track 1 matching. If track 1 can never match, `offset-find` can never confirm
  any candidate offset and always ends in `NoOffsetMatched`.

## Counter-evidence -- verify before changing anything

`Whatinator.Core.Tests/AccurateRip/AccurateRipClientTests.cs:115` documents a
live phase-012 demo against a real disc in `/dev/sr1`, stating that track 1's
checksum -- computed from a real cd-paranoia rip shifted by the drive's actual
+6-sample read offset -- matched **two separate confidence-200 entries** in a
genuine AccurateRip database response (`Fixtures/dBAR-011-00127f7c-00a2b21c-8e0b360b.bin`).

If that value came from `AccurateRipChecksum.Compute`, then the code is right and
the reading above is wrong -- in which case the *trailing* trim is the
inconsistent one. But that test **hardcodes** the literal `0x8cff983d` rather
than computing it, so it cannot adjudicate. Neither can
`AccurateRipChecksumTests`. Note also that an off-by-one in the trim is **not** equivalent
to any read-offset shift -- it adds a single extra term `sample[2939] * 2940` to
the sum -- so a genuine confidence-200 match would be decisive evidence the code
is correct.

What is *not* in doubt: the two ends currently trim different amounts. One of
them is wrong.

## Note from the user

Verify the actual functionality first (against a real disc and the live
database), and then **document the conclusion in the source code either way** --
a comment on `AccurateRipChecksum.Compute` recording which end is authoritative,
what it was checked against, and when. This file is currently the only place
that reasoning exists, and the next person to read `var from = 0;` next to a
comment saying "1-based" will have the same doubt.

## Acceptance Criteria

- [ ] Rip a disc that is well-represented in the AccurateRip database. Confirm
      empirically whether track 1 verifies while tracks 2..n do. That single
      observation settles which end is wrong.
- [ ] Logic changed so the leading and trailing trims exclude the **same** number
      of sample pairs (2940), whichever direction the evidence points.
- [ ] A comment added to `AccurateRipChecksum.Compute` stating the verified
      convention, what real-world data it was confirmed against, and the date --
      so this cannot be re-litigated from first principles again.
- [ ] New test: one track-1 checksum pinned against a value read out of the
      **live AccurateRip database** for a real disc, computed by
      `AccurateRipChecksum.Compute` itself. This is the ground-truth test
      the suite currently lacks entirely.
- [ ] Existing `AccurateRipChecksumTests` constants re-derived if the logic
      changes, with a comment noting they are change-detectors, not ground truth.
- [ ] Re-run `offset-find` against a known drive and confirm it still (or now)
      resolves an offset.
