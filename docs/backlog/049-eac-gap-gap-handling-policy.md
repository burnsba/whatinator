# EAC gap: gap handling is fixed to append-to-previous

**Status:** not started

## Description

**EAC feature:** "You can choose to add the gaps to the previous track ...
Otherwise you can choose either to append the gap to the correct track or to
leave it out" (`gap-technology.txt`).

**Status in whatinator: PARTIAL -- one policy, honestly reported.**

`TocFileParser.Parse` sets track N's `EndFrame` to the next track's index-0 minus
one, so the following gap rides at the end of track N. `WhatinatorEacLog.AppendSettings`
hardcodes `Gap handling: Appended to previous track`, which is an accurate
description of the arithmetic rather than a configurable setting.

Append-to-previous is a defensible default -- it is what most rippers do, and it
is what AccurateRip's checksums assume, so changing it would break verification.
The gap is the absence of the other two options.

## Important constraint

**Changing the policy changes the audio content of every track**, which means
AccurateRip verification would no longer match. Any alternative policy must
either disable AccurateRip verification or clearly mark the rip as unverifiable.
This is the main reason not to do this casually.

## Dependency

Requires real per-track pregap detection -- see the `--fast-toc` backlog item.
With fast TOC only track 1's pregap is known, so no alternative policy can be
applied correctly.

Effort: medium, and higher risk than most items here.

## Acceptance Criteria

- [ ] Full TOC scan available and working first.
- [ ] A gap-handling option added with the three EAC policies: append to previous
      (default, current behaviour), append to next, discard.
- [ ] The rip log's `Gap handling` field reflects the actual policy used.
- [ ] AccurateRip verification skipped (with a clear, prominent explanation in
      both console output and the log) for any policy other than
      append-to-previous.
- [ ] Default behaviour byte-identical to today, verified by comparing checksums
      of a re-rip against a previous rip of the same disc.
- [ ] New tests over frame-range computation for each policy.
- [ ] README and `--help` document the option and the AccurateRip consequence.
