# Sanitize picker input, lower stall-timeout default, README readability, id.txt duration alignment

**Status:** done

## Description

A grab-bag of small fixes found/requested in one sitting:

1. **Stray control bytes corrupting the MusicBrainz/Discogs picker.** A
   terminal that sends Ctrl-V as a raw SYN control byte (rather than honoring
   it as paste) can leave that byte sitting in an otherwise-normal input
   line. `ConsolePicker.PromptForSelectionAsync`'s selection input and
   `MakeReleaseInfoCommand`'s manual MusicBrainz/Discogs URL prompts only
   called `.Trim()`, which strips edge whitespace but not embedded control
   characters -- so a numeric choice, `m`, or a pasted URL could silently
   fail to match with no visible cause.

2. **`--stall-timeout` default was too high for its own worst case.** The
   default was `1200` (20 min) per stalled cd-paranoia attempt, which
   combined with `--retries`' default of 5 meant a genuinely wedged read
   (e.g. the known `--force-overread` hang -- see root `CLAUDE.md` §
   Gotchas) could take hours to give up on a single bad track.

3. **README's `Commands` section was unreadable.** Every command's full flag
   set and prose description was crammed into a single Markdown table cell,
   which both wrapped badly in a terminal/narrow viewer and made individual
   flags hard to find.

4. **`id.txt`'s duration column didn't right-align.** `IdTextFile.AppendTracks`
   padded titles to a common width and appended the raw `m:ss` duration
   after, so a disc mixing single- and double-digit minute counts (e.g.
   `4:04` next to `11:05`) had its `:` separators land in different columns.

## Changes made

- Added `Whatinator.Cli.ConsoleInputSanitizer.Clean(string?)`: strips every
  `char.IsControl` character from the line, then trims surrounding
  whitespace. Wired into `ConsolePicker.PromptForSelectionAsync`'s selection
  read and both `MakeReleaseInfoCommand` manual-override prompts (MusicBrainz
  release URL, Discogs release URL).
- Changed `StallTimeoutSeconds`'s default from `1200` to `120` everywhere it's
  declared (`WhatinatorRipOptions`, `PipelineDiscOptions`,
  `CdParanoiaTrackOptions`, `CliArgumentParsing`'s hardcoded fallback) and in
  the config file default documented in `WhatinatorConfig`, `HelpContent`,
  and the README. Updated the two `CliArgumentParsingTests` cases asserting
  the hardcoded default.
- Rewrote README's `Commands` section: each command now gets its own `####`
  subsection with its full invocation as the heading, a prose summary, and
  every flag as its own bullet, instead of one table row per command. `rip`
  cross-references `pipeline` for the flags they share rather than repeating
  the text.
- `IdTextFile.AppendTracks` now left-pads (`PadLeft`) each formatted duration
  to the widest duration on the disc before appending it, in addition to the
  existing title right-padding. Since seconds are always two digits, padding
  every duration string to one common width necessarily puts every `:` at
  the same column offset regardless of how many minute digits a given track
  has.

## Acceptance Criteria

- [x] Picker selection input and manual MusicBrainz/Discogs URL prompts strip
      non-printable characters, not just edge whitespace.
- [x] `--stall-timeout` default is `120` in every source location and in the
      README, with existing tests updated to match.
- [x] README's per-command documentation reads as prose + bullet list per
      command rather than one wide table cell per command.
- [x] `id.txt` track listings right-align the duration column's `:` across
      mixed single-/double-digit minute counts. New test in
      `IdTextFileTests` covers this (`Format_RightAlignsDurationsWithDifferingMinuteDigitCounts`).
