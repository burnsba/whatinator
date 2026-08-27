# EAC gap: no fast/burst extraction mode and no CLI control over retries

**Status:** done

## Description

**EAC feature:** "A secure, a fast and a burst extraction methods selectable"
(`features-of-eac.txt`), plus configurable error recovery quality
(`extraction-technology.txt`).

**Status in whatinator: PARTIAL -- secure only, with a fixed retry count.**

Every rip does the same thing: a test read, a copy read, a CRC32 comparison, and
up to `CdParanoiaTrackOptions.MaxRetries` retries of the whole cycle.
`WhatinatorEacLog.AppendHeader` hardcodes `Read mode : Secure`.

There is no CLI knob for any of it. `Whatinator.Cli/CommandLineOptions.cs` and
`RipCommand.cs` parse only `--device`, `--dest`, `--disc`, `--releaseinfo`, and
`--keep-wav`.

## When this matters

Rarely, but sharply: a badly damaged disc that you just want *something* off.
Right now the only outcome is grinding through the full test/copy cycle plus
retries on every unreadable track, and the drive can spend a very long time on a
disc that will never verify. A single-pass mode would get the recoverable audio
off quickly and mark the result honestly as unverified.

The inverse is also useful: raising retries above the default for a disc that is
*nearly* readable.

Effort: low. The plumbing is a flag and an existing options field.

## Acceptance Criteria

- [x] `--retries <n>` flag added to `rip` and `pipeline`, threaded to
      `CdParanoiaTrackOptions.MaxRetries`.
- [x] A single-pass mode (`--fast` / `--no-verify`) that skips the second read and
      the CRC comparison.
- [x] In single-pass mode the rip log's `Read mode` field says so -- it must not
      claim `Secure` -- and the Test CRC field is reported as unavailable rather
      than duplicating the copy CRC.
- [x] AccurateRip verification still attempted in fast mode (it is an independent
      check and remains meaningful), with the log making clear that local test/copy
      verification was skipped.
- [x] Defaults unchanged: secure mode, existing retry count.
- [x] Documented in the README and `HelpContent`.
- [x] New tests: the flags produce the expected options; fast mode invokes
      cd-paranoia once rather than twice (see note below -- this last part
      isn't covered by an automated test).

## Implementation notes

Landed together with two additional retry-control knobs the user asked for
while working this item, prompted by a real rip
(`.local/run-stuck.log`) that hung all night at 99% on one track under
`--overread` -- a known upstream cd-paranoia hang (see root `CLAUDE.md` §
Gotchas), which neither this item's `--retries` nor `--no-verify` alone would
have stopped:

- `--max-sector-reads <n>` (config `maxSectorReads`, default 12, `0` =
  infinite): passed straight through to cd-paranoia's own `--never-skip`
  flag, capping how many times it retries one bad sector before giving up and
  moving on, instead of leaving it at cd-paranoia's own unflagged default of
  roughly 20.
- `--stall-timeout <seconds>` (config `stallTimeoutSeconds`, default 1200,
  `0` disables): if a single cd-paranoia invocation (a test or copy read)
  reports no forward progress for this long, `CdParanoiaTrackReader` kills it
  and counts the attempt as failed, letting the existing `--retries` cycle
  (and eventual degraded/warn-and-continue path) take over instead of hanging
  indefinitely. This is a wall-clock stall detector, not a literal
  per-sector retry counter -- cd-paranoia's `--stderr-progress` wire format
  has no such counter to observe, which is also why it's a *timeout*
  rather than a count.
- Both are CLI errors when combined with `--no-verify` (their *defaults*
  still apply underneath fast mode regardless, as a safety net).
- These three settings compose multiplicatively into one track's worst-case
  wall-clock time (`stall_timeout × (1 or 2) × retries`) -- documented in the
  README/`HelpContent` rather than left to be discovered the hard way.
