# EAC gap: --force-overread is plumbed end-to-end but unreachable

**Status:** done

## Description

**EAC feature:** "Overread into Lead-In and Lead-Out" -- reading past the normal
track boundaries, which matters for recovering the final samples of the last
track when a positive read offset is in use. The AccurateRip notes explicitly
recommend enabling it alongside "fill missing samples with silence".

**Status in whatinator: PARTIAL -- fully wired, never enabled.**

The `--force-overread` support exists end-to-end:

- `CdParanoiaTrackOptions.Overread` (default `false`)
- `CdParanoiaTrackReader.BuildStartInfo:271` -- adds the flag when set
- `WhatinatorRipOptions.Overread`, `PipelineDiscOptions.Overread`
- `PipelineRunner.cs:78,118` forwards it
- `EacLogOptions.Overread`, and `WhatinatorEacLog.AppendSettings:107` prints the
  field

But **no caller ever sets it true**:

- `src/Whatinator.Cli/RipCommand.cs:137` -- hardcoded `Overread: false`
- `PipelineCommand` never supplies it, so `PipelineDiscOptions`' default `false` applies
- No CLI flag exists

The only place `Overread: true` appears anywhere in the repo is a unit test
(`CdParanoiaTrackReaderTests.cs:40`).

So the rip log dutifully reports "Overread into Lead-In and Lead-Out: No" on
every rip, because it is structurally impossible for it to say anything else.

Effort: trivial. Add the flag and thread it.

## Caveat worth testing

Not all drives support overreading, and those that do not may error or return
silence. The flag should be opt-in with the failure mode understood, not
defaulted on.

## Acceptance Criteria

- [x] `--overread` flag added to `rip` and `pipeline`, threaded through to
      `WhatinatorRipOptions.Overread` / `PipelineDiscOptions.Overread`.
- [x] Documented in both the README command tables and `HelpContent`.
- [x] The rip log's existing "Overread into Lead-In and Lead-Out" field now
      reflects the real setting.
- [x] Manual verification on a real drive: confirmed `--force-overread` reaches
      cd-paranoia (invoked it directly with the exact flag `CdParanoiaTrackReader`
      builds). This ASUS DRW-24F1ST does **not** support it cleanly -- see root
      `CLAUDE.md` § "External tools misbehave in specific, known ways" for the
      full finding (it hangs rather than erroring). Left with no `overreads`
      entry in this dev machine's config as a result.
- [x] Added a per-drive config key: `WhatinatorConfig.Overreads` /
      `GetOverread`, keyed the same way as `ReadOffsets`/`CacheDefeats`. A
      `true` entry makes `rip`/`pipeline` pass `--force-overread` without
      needing `--overread` on every invocation; `--overread` still forces it
      on for a single run regardless of the map.
- [x] Test coverage: `WhatinatorConfigTests` covers `GetOverread`'s null-map/
      no-entry/matching-entry/round-trip cases (mirroring the existing
      `GetCacheDefeat` tests) -- the actual new logic here. The CLI-level
      wiring in `RipCommand`/`PipelineCommand` stays untested per this
      project's existing convention (root `CLAUDE.md`: those files "still need
      a drive/network and stay untested"); `CdParanoiaTrackReaderTests`
      already covered `--force-overread` reaching the process argument list
      before this change.
