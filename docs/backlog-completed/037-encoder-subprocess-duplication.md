# FlacEncoder and LameEncoder duplicate their process-drive body; LameEncoder has no interface

**Status:** done

## Description

`src/Whatinator.Core/Flac/FlacEncoder.cs:32-41` and
`src/Whatinator.Core/Mp3/LameEncoder.cs:35-45` have the same six-line
process-drive body, differing only in the `capturedError` `MemoryStream`. Both
`BuildStartInfo` methods have the same "add tag flags, skipping null Year/Genre"
shape over the same nine metadata fields.

There is also an **asymmetry**: `FlacEncoder : IFlacEncoder`, but `LameEncoder`
has no interface. So `Mp3Packager` news one up directly (`Mp3Packager.cs:31`) and,
unlike `WhatinatorRipRunner`, has no test seam for the encode path. Every other
external dependency in Core is behind an interface and faked in the tests --
this is the one exception.

## Proposed fix

Extract a `SubprocessRunner.RunAsync(ProcessStartInfo, Stream stdout, Stream stderr, Stream? capture, CancellationToken)`.

This is also the single natural place to implement kill-on-cancel (see the Ctrl-C
backlog item) for all six subprocess wrappers at once, rather than six times.

## Acceptance Criteria

- [ ] Shared `SubprocessRunner` used by `FlacEncoder`, `LameEncoder`,
      `CdParanoiaTrackReader` (both the cd-paranoia and sox paths),
      `CdrdaoTocReader`, `CacheDefeatAnalyzer`, and `CoverArtProcessor`.
- [ ] Kill-on-cancel implemented once inside it.
- [ ] Stream draining implemented once inside it -- which also fixes the
      `CoverArtProcessor` deadlock.
- [ ] `ILameEncoder` added and `Mp3Packager` takes it via constructor, matching
      the `IFlacEncoder` / `WhatinatorRipRunner` pattern.
- [ ] New `Mp3PackagerTests` case using a fake encoder, exercising the packaging
      sequence without spawning `lame` -- currently impossible.
- [ ] Existing `FlacEncoderTests` / `LameEncoderTests` `BuildStartInfo` assertions
      unchanged (pure refactor of the drive body, not the argument construction).
