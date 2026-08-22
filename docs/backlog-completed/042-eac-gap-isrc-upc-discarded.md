# EAC gap: ISRC and UPC are captured and then discarded

**Status:** done

## Description

**EAC feature:** cue sheets and logs carrying UPC and ISRC
(`features-of-eac.txt`).

**Status in whatinator: PARTIAL** -- parsed, printed by one diagnostic command,
then thrown away.

- **ISRC:** `TocFileParser` handles the `ISRC` statement into
  `DiscTocTrack.Isrc`. Printed by `toc` (`TocFormatter.Print`). Never written to
  the rip log, `id.txt`, FLAC/MP3 tags, `releaseinfo.json`, or a cue sheet.
- **UPC/EAN:** the disc's `CATALOG` line is parsed into `DiscToc.CatalogNumber`
  and echoed live by `CdrdaoTocReader` ("Found disk catalogue number: ..."), then
  discarded. Note `IdTextFile.FormatRelease` uses the **MusicBrainz label catalog
  number**, which is a different thing entirely; `MbRelease.Barcode` is used only
  as a Discogs search key and never persisted.

This is metadata fidelity being lost for free -- the expensive part (reading it
off the disc) is already done.

## Dependency

ISRC is read from the sub-channel, so in practice it needs the full TOC scan --
see the `--fast-toc` backlog item. UPC/`CATALOG` comes from the raw TOC and is
available even in fast mode, so **the UPC half can be done immediately**.

## Acceptance Criteria

- [ ] `DiscToc.CatalogNumber` (UPC/EAN) persisted: written to the rip log, and
      added to `id.txt` as a distinct field clearly separate from the MusicBrainz
      label catalog number.
- [ ] Per-track ISRC written to the rip log and into FLAC tags
      (`ISRC=` is a standard Vorbis comment field).
- [ ] Both carried into the cue sheet when that lands.
- [ ] Decision recorded on whether they belong in `releaseinfo.json` -- that file
      is editorial metadata from MusicBrainz/Discogs, while these are physical
      facts about the pressing, so a separate section or file may be cleaner.
- [ ] Values reported as absent rather than blank when the disc carries none.
- [ ] New tests: log and `id.txt` output with and without a catalog number;
      FLAC tag arguments include ISRC when known and omit it when not.
- [ ] Manual verification against a real disc known to carry ISRCs, cross-checked
      against `cdrdao read-toc` output for the same disc.
