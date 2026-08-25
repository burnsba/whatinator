# What is "track quality"?

The rip log's per-track `Track quality {X.X} %` line (see
`WhatinatorEacLog.AppendTracks`) reports a number computed by
`CdParanoiaTrackReader.ComputeQuality`. This doc explains what that number
means, where it comes from, and -- importantly -- what it does *not* tell you.

## Where the number comes from

`cd-paranoia` is invoked with `--stderr-progress`
(`CdParanoiaTrackReader.BuildStartInfo`), which makes it emit a stream of
`##: read @ <wordOffset>`-shaped progress lines to stderr as it scans and
rereads sectors during a single read pass -- `cd-paranoia` writes *all* of its
output, including this progress and its `--version` banner, to stderr, never
stdout (see root `CLAUDE.md` § Gotchas).

`ComputeQuality` parses those lines from the **test** read's captured stderr
(the first of the two reads in whatinator's test/copy cycle -- see
`CdParanoiaTrackReader.TryOnceAsync`), not the copy read. Each parsed line
reports an absolute disc word offset. Because `cd-paranoia`'s progress output
is always in absolute disc terms rather than track-relative terms (confirmed
live against a real drive -- see `CdParanoiaProgressReporter.BeginRead`'s doc
comment), each offset is converted to a track-relative frame via
`CdParanoiaProgressLine.ToTrackRelativeFrame` before anything is counted --
exactly the same conversion `CdParanoiaProgressReporter.Feed` performs for
live progress display. Getting this conversion wrong was a real, once-shipped
bug (see `docs/backlog-completed/010-computequality-absolute-vs-relative-frames.md`):
without it, quality read as a meaningless, saturated 100% for every track
after the first.

## The formula

In a clean read with no rereads, `cd-paranoia` is expected to touch each
frame of the track exactly twice: once during the forward scan, once during
its own internal verification pass. `ComputeQuality` sums how many
track-relative frames were covered across all parsed progress lines
(`reads`), then computes:

```
quality = min(frameCount * 2.0 / reads, 1.0)
```

A track that required extra rereading -- because of jitter, a scratch, or any
other reason `cd-paranoia` had to re-scan sectors -- accumulates more than
`2 * frameCount` reads, which pushes this ratio, and therefore the reported
quality percentage, below 100%. If no parseable progress lines were captured
at all, the method returns `null` (rendered in the log as `not available`,
not `100.0 %` -- a uniformly-100%-looking field would misrepresent an
unmeasured track as a verified-perfect one).

This is a faithful port of a publicly documented reference implementation of
this same idea, and deliberately omits that source's own read-but-never-used
extra accounting state -- see root `CLAUDE.md` § "Ported algorithms: don't
'fix' the reference implementations" for why that state is left out on
purpose rather than treated as a bug.

## What it does *not* mean

Track quality is an **effort metric for one read pass** -- how much
rereading the drive needed to produce data it considered acceptable for that
track. It is not a statement about whether the resulting audio is correct.

Correctness is established by two entirely separate mechanisms (root
`CLAUDE.md` § "Verification is two independent mechanisms"), and this metric
is not one of them:

1. **Test/copy CRC32 compare**: the track is read twice, independently, to
   separate temporary files, and their CRC32s must match
   (`CdParanoiaTrackReader`). This is what actually gates whether a track is
   accepted; a mismatch triggers a full retry cycle up to
   `WhatinatorRipOptions.MaxRetries`, independent of the quality figure.
2. **AccurateRip lookup**: after every track on the disc has been read, a
   whole-disc AccurateRip checksum lookup compares against other people's
   rips of the same pressing (`AccurateRipClient`).

A track can report a low quality percentage (lots of rereading needed) and
still pass both of the above -- byte-perfect, verified, just harder-won. The
FLAC that gets packaged carries no trace of this number at all; it is purely
an archival detail in the rip log (`WhatinatorEacLog`), the same role it
plays in a real EAC log.

## How `cd-paranoia` factors in

`cd-paranoia` is the actual extraction tool driving the disc; it does not
report a single "quality" figure of its own. This metric is whatinator's own
derivation from `cd-paranoia`'s progress chatter, filling the role EAC's
native per-track quality percentage plays in its own logs (computed from
EAC's own drive-level retry accounting, not from anything `cd-paranoia`
specific -- whatinator's version is necessarily a different computation
because it's driving a different underlying tool).
