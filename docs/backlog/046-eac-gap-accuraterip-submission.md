# EAC gap: AccurateRip results are never submitted back to the database

**Status:** not started

## Description

**EAC feature:** "on each rip some offset and checksum data is generated, which
can be send to the AccurateRip database using the AccurateRip menu interface"
(`accuraterip.txt`). The notes frame submission as how the database stays useful:
"of course you should help to populate it further."

**Status in whatinator: MISSING.** `AccurateRipClient` is GET-only --
`grep -rn 'PostAsync\|HttpMethod.Post' src/` returns nothing. whatinator
consumes the database and contributes nothing back.

Everything needed to submit is already computed: both disc IDs, the CDDB disc ID,
and per-track v1/v2 checksums.

## Open question -- this may be a deliberate "no"

Two reasons to think hard before building it:

1. The AccurateRip submission protocol is not publicly documented; it would have
   to be reverse-engineered, and it can change without notice.
2. The project has an existing stated boundary against CUETools-style services
   (`WhatinatorEacLog` carries a "never CUETools" comment, and the log
   deliberately omits a CUETools DB section). Submission may fall on the same side
   of that line.

Note also that submitting checksums derived from a **wrong** implementation would
pollute a shared public resource -- so the AccurateRip trim-asymmetry item must be
conclusively resolved before any submission code is even prototyped.

Effort: medium-high.

## Acceptance Criteria

- [ ] Decision recorded: build it, or close this as a deliberate non-goal with the
      reasoning written down (in which case the "why not" belongs in the root
      `CLAUDE.md` alongside the CUETools note).

If building it:

- [ ] The AccurateRip trim question resolved and verified against the live
      database first -- do not submit checksums from an unverified implementation.
- [ ] Submission is **opt-in**, never automatic, and never silent.
- [ ] The submission protocol documented in a decision doc, with the source of the
      information recorded.
- [ ] Only fully-verified, non-degraded rips are eligible to submit.
- [ ] New tests over request construction using `StubHttpMessageHandler`; no test
      may perform a real submission.
- [ ] Manual verification that a submitted disc subsequently appears in a lookup.
