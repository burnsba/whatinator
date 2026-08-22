# EAC gap: no fast/burst extraction mode and no CLI control over retries

**Status:** not started

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

- [ ] `--retries <n>` flag added to `rip` and `pipeline`, threaded to
      `CdParanoiaTrackOptions.MaxRetries`.
- [ ] A single-pass mode (`--fast` / `--no-verify`) that skips the second read and
      the CRC comparison.
- [ ] In single-pass mode the rip log's `Read mode` field says so -- it must not
      claim `Secure` -- and the Test CRC field is reported as unavailable rather
      than duplicating the copy CRC.
- [ ] AccurateRip verification still attempted in fast mode (it is an independent
      check and remains meaningful), with the log making clear that local test/copy
      verification was skipped.
- [ ] Defaults unchanged: secure mode, existing retry count.
- [ ] Documented in the README and `HelpContent`.
- [ ] New tests: the flags produce the expected options; fast mode invokes
      cd-paranoia once rather than twice.
