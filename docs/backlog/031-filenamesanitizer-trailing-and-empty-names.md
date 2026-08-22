# FileNameSanitizer does not trim trailing dots/spaces or handle an empty result

**Status:** not started

## Description

`src/Whatinator.Core/Naming/FileNameSanitizer.cs:12-26`

The sanitizer correctly neutralises path traversal -- both `/` and `\` become
`_`, so the container and track naming paths are safe. That part is sound.

What it does not do:

- Trim trailing dots or spaces. These are legal on Linux but problematic on
  Windows/SMB shares and confusing in general -- relevant since release folders
  routinely get copied to a NAS.
- Handle an all-blank or empty result. A MusicBrainz release with an empty title
  yields a folder named `" -  [flac 0000]"`.

Cosmetic, but cheap to fix, and the naming path is the one place a bad value
becomes a permanent artifact on disk.

## Acceptance Criteria

- [ ] Leading/trailing whitespace and trailing dots trimmed from the result.
- [ ] An empty or all-blank result replaced with a documented placeholder
      (e.g. `"unknown"`), not returned as-is.
- [ ] Reserved Windows device names considered (`CON`, `PRN`, `NUL`, `AUX`,
      `COM1`..`LPT9`) if cross-platform copies matter -- or explicitly documented
      as out of scope.
- [ ] New tests in `FileNameSanitizerTests`: trailing dot, trailing space, empty
      input, whitespace-only input.
- [ ] Verify existing folder names are unaffected -- the change must not rename
      anything already on disk.
