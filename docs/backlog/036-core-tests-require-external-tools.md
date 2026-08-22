# Core tests fail rather than skip when ffprobe, sox, or magick is missing

**Status:** not started

## Description

Several `Whatinator.Core.Tests` tests shell out to real external tools to inspect
real encoder output:

- `Flac/FlacEncoderTests.cs:111` -- `ffprobe`
- `Flac/FlacEncoderTests.cs:151` -- `sox`
- `Mp3/LameEncoderTests.cs:147` -- `ffprobe`
- `Mp3/LameEncoderTests.cs:187` -- `sox`
- `Mp3/Mp3PackagerTests.cs:209` -- `sox`
- `Mp3/Mp3PackagerTests.cs:233` -- `magick`
- `CoverArt/CoverArtProcessorTests.cs:119` -- `magick`

None of them probe for the tool first, so on a machine without it the test
**fails** rather than skipping. `ffprobe` (package `ffmpeg`) is not otherwise a
whatinator dependency at all -- it is used purely as an independent verifier of
encoder output -- so a contributor has no reason to expect it.

The README now documents this, but documentation is a workaround, not a fix.

## Acceptance Criteria

- [ ] A shared test helper probes `PATH` for a named tool and skips with a clear
      reason when absent (xunit `Assert.Skip` in v2.9+, or a `[SkippableFact]`
      trait).
- [ ] All seven sites use it.
- [ ] `dotnet test` on a machine with only the required runtime tools installed
      passes with skips rather than failures.
- [ ] README's testing note updated to say these tests skip (rather than fail)
      when the verifier tools are absent.
