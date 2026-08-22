# EAC gap: CD-Text is skipped and never parsed

**Status:** not started

## Description

**EAC feature:** CD-Text support, including carrying it into cue sheets
(`features-of-eac.txt`).

**Status in whatinator: MISSING, by design.**
`src/Whatinator.Core/Toc/TocFileParser.cs` skips `CD_TEXT` blocks wholesale by
brace-depth counting, with an explicit comment: "there is no consumer for that
data yet".

## Value assessment

Modest. MusicBrainz and Discogs supply better, richer, correctable metadata than
CD-Text does, and whatinator already uses both. CD-Text matters mainly for:

- **Cue sheet fidelity** -- EAC writes CD-Text into the cue, and a reconstruction
  that drops it is not quite exact.
- **Obscure or unlisted discs** -- a promo, a local pressing, or anything absent
  from MusicBrainz, where CD-Text may be the only on-disc metadata available.
- **Cross-checking** -- disagreement between CD-Text and MusicBrainz is a useful
  signal that the wrong release was matched.

## Design question

If implemented, precedence must be decided and documented: MusicBrainz almost
certainly wins for tagging, with CD-Text used as a fallback for unmatched discs
and as a cross-check. It should **not** silently override editorial metadata the
user may have corrected in `releaseinfo.json`.

Effort: medium -- parse the block, model it, thread it through, decide precedence.

## Acceptance Criteria

- [ ] `TocFileParser` parses `CD_TEXT` blocks into a model rather than skipping
      them (`TITLE`, `PERFORMER`, `SONGWRITER`, `ISRC`, `UPC_EAN` at minimum, at
      both disc and track level).
- [ ] Precedence rule decided, implemented, and documented: what wins when CD-Text
      and MusicBrainz disagree.
- [ ] CD-Text surfaced in `toc` output.
- [ ] Used as a fallback when MusicBrainz returns no match, so a `disc-info` on an
      unlisted disc still shows real titles.
- [ ] Carried into the cue sheet when that lands.
- [ ] Existing behaviour preserved for discs without CD-Text (the overwhelming
      majority).
- [ ] New tests: a `.toc` fixture containing a CD-Text block parses correctly; one
      without still parses; a malformed block is handled per the parser's
      strictness policy.
- [ ] The root `CLAUDE.md` "Not supported, on purpose" entry for CD-Text updated.
