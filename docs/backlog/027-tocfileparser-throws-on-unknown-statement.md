# TocFileParser aborts the whole rip on any unrecognised .toc statement

**Status:** not started

## Description

`src/Whatinator.Core/Toc/TocFileParser.cs:117-118`

Any keyword in a `cdrdao read-toc` output file outside the hard-coded list
throws `FormatException`, aborting the parse.

This is deliberate and the class documents it -- failing loudly beats silently
misreading a disc's geometry, and the grammar was spiked against real output
plus `man cdrdao`'s "TOC FILES" section.

The risk is upstream drift: a future cdrdao release emitting one new statement
would break `toc`, `rip`, and `pipeline` **outright**, on discs that read
perfectly well, with an error that reads like a corrupt disc rather than a
version mismatch. Since cdrdao is a hard dependency and users will upgrade it
independently of whatinator, this is a plausible future support burden.

## Suggested approach

Distinguish two cases:

- An **unrecognised statement** at the top level -- skip it, emit a warning to
  stderr naming the keyword, and continue. Nothing whatinator reads is affected
  by a statement it does not know about (`CD_TEXT` blocks are already skipped
  wholesale on exactly this reasoning).
- A **malformed known statement** -- keep throwing. This is the case where
  silently continuing could produce wrong frame arithmetic.

## Acceptance Criteria

- [ ] Unknown top-level statements skipped with a warning rather than throwing.
- [ ] Malformed known statements still throw, with the existing message quality.
- [ ] The warning names the unrecognised keyword and suggests it may be a newer
      cdrdao -- so a user can report it usefully.
- [ ] New tests: a `.toc` containing a fabricated unknown statement parses
      successfully and produces a warning; a `.toc` with a malformed `TRACK` line
      still throws.
- [ ] Class doc comment updated to describe the new two-tier behaviour.
