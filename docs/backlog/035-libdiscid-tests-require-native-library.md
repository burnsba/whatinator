# Whatinator.LibDiscId.Tests fails rather than skips without libdiscid installed

**Status:** not started

## Description

Three tests cross into native code:

- `DiscReaderTests.cs:31-45` -- `GetNativeVersion_ReturnsNonEmptyString`,
  `GetDefaultDevice_ReturnsNonEmptyString`
- `DiscReaderTests.cs:16-28` -- `Read_ThrowsDiscIdExceptionForNonexistentDevice`

On any machine without `libdiscid0` -- a CI container, a contributor's Mac --
`dotnet test` fails these three with `DllNotFoundException` rather than skipping
cleanly. The README's build instructions say to run `dotnet test` with no
mention that a native library must be present for it to pass.

Separately, the comment at `DiscReaderTests.cs:20-21` claims the
nonexistent-device test "fails before any device is opened". That is inaccurate:
`discid_read_sparse` calls `open()` on the path and fails there, so the test does
perform a real syscall and does load the native library. The comment should say
so, since it is currently the reason someone might assume the test is hermetic.

## Acceptance Criteria

- [ ] The three native-touching tests gated behind a probe --
      `NativeLibrary.TryLoad("libdiscid.so.0", out _)` -- skipping with a clear
      reason rather than failing.
- [ ] The inaccurate comment at `DiscReaderTests.cs:20-21` corrected.
- [ ] README notes which tests need which native dependencies (see the companion
      item for `ffprobe`/`sox`/`magick` in `Whatinator.Core.Tests`).
- [ ] Verified by running `dotnet test` in an environment without `libdiscid0`:
      skips, does not fail.
