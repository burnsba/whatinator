# AccurateRip match is labelled v2 without anything identifying the version

**Status:** not started

## Description

`src/Whatinator.Core/AccurateRip/AccurateRipClient.cs:144-153` (`MatchTrack`)

Each 9-byte track record in a dBAR response holds one confidence byte and one
CRC (with four further bytes currently dropped). That single CRC is tested
against **both** `computed.V1` and `computed.V2`.

`WhatinatorEacLog.FormatAccurateRipLine:302-311` then prefers the V2 branch and
labels the log line `(AR v2)`.

So the label is not derived from anything that actually identifies which
algorithm version the served CRC represents -- it is derived from the order the
code checks in.

Practically this is cosmetic: a v1 CRC being reported as v2 requires a 2^-32
collision between the disc's own v1 and v2 values. But the log claims a fact it
does not know, and the four dropped bytes of each record may well be where the
distinguishing information lives.

## Acceptance Criteria

- [ ] The four currently-dropped bytes of each track record documented -- what
      they are per the dBAR format -- with a comment either using them or stating
      why they are ignored.
- [ ] Either derive the v1/v2 label from the response, or change the log wording
      so it does not assert a version the code cannot determine.
- [ ] Comment on `MatchTrack` recording that one CRC is compared against both
      computed values and why that is acceptable.
- [ ] New test pinning the label for a match that is v1-only and one that is
      v2-only.
