# ComputeQuality mixes absolute and track-relative frame offsets

**Status:** not started

## Description

`src/Whatinator.Core/Rip/CdParanoiaTrackReader.cs:181-233`, called at `:403`.

`CdParanoiaProgressReporter.BeginRead`'s doc (`Rip/CdParanoiaProgressReporter.cs:63-71`)
states -- confirmed live against a real drive -- that cd-paranoia's
`##: ... @ <wordOffset>` values are **absolute disc offsets**, not track-relative.
That is why `Feed` at `:111` does:

```csharp
frame = (wordOffset / WordsPerFrame) - _startFrame;
```

`ComputeQuality` performs no such subtraction, yet is invoked with
track-relative bounds:

```csharp
var quality = ComputeQuality(testRun.CapturedStandardError, 0, stopOffset);
// stopOffset = EndFrame - StartFrame
```

## Failure scenario

For track 2 of the Glorilla TOC (start frame 6835, stop offset 9832): the first
parsed `read` line reports an absolute frame of about 6835, so `markStart=0,
markEnd=6835` credits 6835 phantom reads in a single step. Every subsequent line
is clamped at `stop + 1` and contributes nothing. `reads` lands at about 9833,
which equals `frameCount`, so
`min(frameCount * 2.0 / reads, 1.0)` saturates at **1.0**.

Net effect: the quality metric reads 1.0 for every track except track 1, on
every disc. It is silently meaningless.

## Currently latent

`Quality` flows from `CdParanoiaTrackResult.Quality` into
`WhatinatorTrackRipResult.Quality` and is **never rendered anywhere**.
`WhatinatorEacLog.AppendTracks` emits Filename, Pre-gap, Peak, Extraction speed,
Test CRC, Copy CRC, AccurateRip, and Copy OK -- but no quality line. So this is
dead computed state carrying a latent bug.

That makes the ordering important: **fix the frame arithmetic before adding the
log line**, not after. See the separate EAC-gap backlog item for surfacing
quality in the log.

## Why the tests miss it

All three `ComputeQuality` tests use `start: 0` -- precisely the case where the
bug cannot manifest.

## Acceptance Criteria

- [ ] `ComputeQuality` receives the track's absolute `StartFrame` and subtracts
      it from each parsed frame offset, exactly as `Feed` does.
- [ ] Better: both `Feed` and `ComputeQuality` share a single
      "absolute word offset -> track-relative frame" helper, so the two cannot
      diverge again.
- [ ] New test with a **non-zero** track start frame, asserting a quality value
      strictly below 1.0 for input representing re-reads. This is the case the
      current suite has no coverage for at all.
- [ ] Manual verification against a real rip: quality values vary per track and
      are not uniformly 1.0.
- [ ] Comment added recording that cd-paranoia offsets are absolute, referencing
      `CdParanoiaProgressReporter`'s live-confirmed note.
