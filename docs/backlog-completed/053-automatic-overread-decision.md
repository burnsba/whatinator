# Scope --overread to the disc's boundary track, and add --skip-overread-on-stall

**Status:** done

## Description

Backlog-completed 050 confirmed `--force-overread` reliably hangs cd-paranoia
on at least one drive when combined with a positive read offset on the
disc's last track. The README/`--help` text that followed told the user to
manually judge when `--overread` is "worth it" -- but gave no way to actually
know, and no story for what to do if the recommended choice still hangs.

Overreading only ever matters for the one track touching the physical edge
of the disc that the configured read offset shifts into: the **last** track
for a positive offset, the **first** track for a negative one. Middle tracks
are never affected either way -- `CdParanoiaTrackReader.BuildStartInfo`
currently passes `--force-overread` uniformly to every track when `Overread`
is set, but it's a no-op on every track except that one boundary track.

Without overreading, cd-paranoia doesn't error on the missing boundary
samples -- it silence-fills them (already reflected in the rip log's "Fill
up missing offset samples with silence: Yes" line).

An earlier draft of this item proposed deciding automatically whether to
overread based on a sample-offset threshold. Rejected: `--overread` stays a
plain, user-controlled flag with no automatic/forced distinction and no
threshold setting -- see this item's implementation notes once done.

## Desired behavior

1. When `--overread` is given, only ever pass `--force-overread` to the one
   boundary track it can possibly affect (by the sign of the read offset),
   not every track. No behavior change for the boundary track itself; every
   other track simply stops being asked to do something that was always a
   no-op for it.
2. Print (console) and record (rip log) which track, if any, overread was
   actually applied to -- including the case where `--overread` was given
   but had no effect (offset is 0, or the boundary track is a data track).
3. Add `--skip-overread-on-stall`, used together with `--overread`: if the
   boundary track's overread attempt hits `--stall-timeout`, stop retrying
   with overread on (it will just stall again) and retry the track's
   remaining attempts with overread off, accepting a silence-filled
   boundary. Without the flag, a stalled overread attempt marks that one
   track `Degraded` (rip continues) with a warning naming the flag.

## Acceptance Criteria

- [x] `--overread`'s effect is scoped to the disc's boundary track only.
- [x] The applied-track decision (or lack of effect) is visible in both
      console output and the saved rip log.
- [x] `--skip-overread-on-stall` added to `rip`/`pipeline`; without it, a
      stalled overread attempt degrades just that track with an actionable
      warning; with it, the track's remaining retries drop overread and
      finish silence-filled instead.
- [x] README/`HelpContent` updated to describe the boundary-track scoping
      and the new flag.
- [x] Tests: boundary-track resolution (both offset signs, offset 0), the
      retry loop's early-exit-on-stall behavior, and the degraded-reason
      message.
