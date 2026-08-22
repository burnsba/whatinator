# EAC gap: no per-track suspicious positions or error locations

**Status:** not started

## Description

**EAC feature:** "Output of time positions of all non-exact corrections and the
possibility to listen to these positions" (`features-of-eac.txt`); burst mode
"will tell the user in the status report by showing up suspicous positions"
(`extraction-technology.txt`).

**Status in whatinator: MISSING** (the listen-to-them half is GUI-only and out of
scope; the reporting half is not).

whatinator detects problems at **whole-track** granularity: the test/copy CRC32
comparison either matches or it does not, and the track is retried or marked
degraded. The rip log records `Copy OK` or a degradation, but never *where* in
the track the drive struggled.

The data is already captured. `CdParanoiaTrackReader.TryOnceAsync` retains
cd-paranoia's full stderr, and `ComputeQuality` already parses the `##:` progress
lines out of it -- it just aggregates them into a single number and throws the
positions away.

## Value

For a scratched disc this is the difference between "track 7 is degraded" and
"track 7 had re-reads clustered at 2:31-2:34" -- which tells you whether the
damage is a localised scratch worth cleaning, and which part of the audio to
listen to.

## Dependency

Depends on the `ComputeQuality` frame-arithmetic fix -- the same absolute-vs-
relative offset bug would put any reported positions in the wrong place.

Effort: medium.

## Acceptance Criteria

- [ ] The progress parser retains re-read spans (start/end frames) rather than
      only a count.
- [ ] Frames converted to `mm:ss.ff` positions **track-relative**, using the same
      corrected offset arithmetic as the quality fix.
- [ ] A per-track section added to the rip log listing suspicious positions,
      following EAC's format, omitted entirely when a track read cleanly.
- [ ] Adjacent spans coalesced so a single rough patch is one entry, not hundreds.
- [ ] A cap on how many are listed, with a count of the remainder, so a badly
      damaged disc cannot produce a megabyte of log.
- [ ] New tests over span extraction from captured cd-paranoia stderr fixtures,
      including a non-zero track start frame.
- [ ] Manual verification against a disc with a known localised defect.
