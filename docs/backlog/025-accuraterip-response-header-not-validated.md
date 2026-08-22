# AccurateRip response headers are skipped, so a wrong-disc response would be accepted

**Status:** not started

## Description

`src/Whatinator.Core/AccurateRip/AccurateRipClient.cs:214-242` (`ParseEntries`)

Each entry in a dBAR response begins with a 12-byte header carrying the
response's own `discId1`, `discId2`, and `cddbId`. The parser **skips these bytes
entirely** and proceeds straight to the track records.

Consequence: a response for a *different* disc -- a server-side mismatch, a
caching proxy serving a stale or wrong body, a truncated/misaligned read --
would be matched against our computed checksums as though it were ours. Since the
checksums almost certainly would not match, the practical outcome is a false
"cannot be verified" rather than a false "accurate". But it is silent, and it
removes a free self-check the format was designed to provide.

The disc IDs are already computed at the call site (`AccurateRipDiscId.Compute`,
`CddbDiscId.Compute`) in order to build the request URL, so validating costs
nothing.

## Acceptance Criteria

- [ ] Parse the 12 header bytes per entry into `discId1` / `discId2` / `cddbId`.
- [ ] Compare against the computed IDs for the disc being verified; skip entries
      that disagree, and surface a warning if **every** entry disagrees (which
      indicates a wrong-disc response rather than a normal miss).
- [ ] New test using a hand-modified copy of the real fixture
      (`Fixtures/dBAR-011-00127f7c-00a2b21c-8e0b360b.bin`) with an altered disc ID:
      assert the entry is rejected rather than matched.
- [ ] The real fixture test continues to pass unchanged.
