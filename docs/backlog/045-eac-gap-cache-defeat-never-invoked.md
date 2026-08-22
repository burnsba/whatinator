# EAC gap: CacheDefeatAnalyzer is fully built but has no CLI command

**Status:** not started

## Description

**EAC feature:** automatic drive feature detection -- specifically, whether the
drive caches audio data (`automatic-feature-detection.txt`).

**Status in whatinator: PARTIAL -- implemented, tested, and unreachable.**

`src/Whatinator.Core/Drive/CacheDefeatAnalyzer.cs` runs `cd-paranoia -A` and
classifies the result as `CanDefeat` / `CannotDefeat` / `Unknown`. It has its own
test file (`Drive/CacheDefeatAnalyzerTests.cs`, six tests).

But `CacheDefeatAnalyzer.AnalyzeAsync` has **zero production callers**. The only
references outside the class are its own tests and doc-comment cross-references.
`RipEnvironmentResolver` reads the *config value* into the log; nothing ever
computes it.

The README currently instructs the user to run `cd-paranoia -A` by hand and
transcribe the answer into `config.json`'s `cacheDefeats` map -- which is exactly
the manual step `offset-find` was built to eliminate for `readOffsets`.

`WhatinatorConfig`'s own doc comment describes this as the pre-automation state,
mirroring what `readOffsets` looked like before phase 017.

## Scope suggestion

A `cache-check` command mirroring `offset-find`: run the analysis against the
current drive, print the classification, and write it into the config under the
drive's `DriveKey`. Effort: roughly an hour, since the analysis and the config
write path both already exist.

## Acceptance Criteria

- [ ] A `cache-check [--device <path>]` command added, calling
      `CacheDefeatAnalyzer.AnalyzeAsync` and saving the result to
      `WhatinatorConfig.CacheDefeats` under the current drive's key -- same shape
      as `offset-find`'s write to `readOffsets`.
- [ ] Overwrites any prior entry for that drive, and says so.
- [ ] Warns that the analysis takes real drive time (a full read/timing pass over
      the disc) before starting.
- [ ] Registered in `CommandDispatcher`, documented in `HelpContent` **and** the
      README, in the Setup section next to `offset-find`.
- [ ] README's `cacheDefeats` config row updated -- it currently says to populate
      by hand.
- [ ] The rip log's "Defeat audio cache" field verified to reflect the stored
      result.
- [ ] Manual verification against a real drive.
