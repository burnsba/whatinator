# EAC gap: --force-overread is plumbed end-to-end but unreachable

**Status:** not started

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

- [ ] `--overread` flag added to `rip` and `pipeline`, threaded through to
      `WhatinatorRipOptions.Overread` / `PipelineDiscOptions.Overread`.
- [ ] Documented in both the README command tables and `HelpContent`.
- [ ] The rip log's existing "Overread into Lead-In and Lead-Out" field now
      reflects the real setting.
- [ ] Manual verification on a real drive: confirm the flag reaches cd-paranoia,
      and record in a comment or the README whether this drive supports it.
- [ ] Consider a per-drive config key alongside `readOffsets` / `cacheDefeats`,
      since overread capability is a property of the physical drive -- same
      keying as `WhatinatorConfig.DriveKey`.
- [ ] New test asserting the CLI flag produces `Overread: true` in the options.
