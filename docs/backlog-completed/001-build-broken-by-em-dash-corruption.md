# Build broken by em-dash corruption

**Status:** done

## Description

The tree as checked into this repo did not compile. Every em dash (U+2014) in
the repository had been replaced with a literal ASCII `--`. That is harmless in
C# prose comments -- which is where the vast majority of them live -- but fatal
in two other contexts:

**Illegal XML comments** (`MSB4025: An XML comment cannot contain '--'`):

- `Directory.Build.props:9` -- `<!-- Single source of truth for whatinator's own version -- read at ...`
- `Directory.Build.props:13` -- `... is a git repo -- suppressed so ...`
- `src/Whatinator.Core/Whatinator.Core.csproj:8` -- `<!-- CRC32 for phase 014's cd-paranoia track runner (Test/Copy CRC) --`
- `src/Whatinator.Core.Tests/Whatinator.Core.Tests.csproj:24` -- `... hands-on demo -- see AccurateRipClientTests. -->`

**Illegal char literals** (`CS1012: Too many characters in character literal`):

- `src/Whatinator.Core/IdTextFile.cs:30` -- the `NonStandardDashes` array read
  `['‐', '‑', '‒', '–', '--', '―', '−']`. The
  neighbours are U+2010/2011/2012/2013 and U+2015/2212, so the corrupted entry
  is unambiguously U+2014 EM DASH. The corruption landed inside the very array
  whose job is normalizing non-standard dashes.
- `src/Whatinator.Core.Tests/IdTextFileTests.cs:99` -- `Assert.DoesNotContain('--', text);`, same cause.

This is almost certainly an artifact of how this copy of the tree was produced
(a find/replace or encoding pass during the copy into the clean repo) rather
than something hand-typed.

## Resolution

Fixed. U+2014 restored in the two char literals; the four XML comments changed
to a single `-` separator (valid XML, and consistent with surrounding prose).

Build is clean with 0 warnings; 373 tests pass (357 `Whatinator.Core.Tests`,
16 `Whatinator.LibDiscId.Tests`).

## Acceptance Criteria

- [x] `dotnet build` succeeds with 0 errors and 0 warnings.
- [x] `dotnet test` passes.
- [ ] Audit whatever produced this copy of the tree. The same transform may have
      silently altered em dashes inside **string literals** that still compile --
      those would be behavioural changes with no compiler error to flag them.
      Grep for `--` inside string literals in log/report formatters
      (`WhatinatorEacLog`, `IdTextFile`, `Mp3LogFile`, `HelpContent`) and compare
      against `example/` reference output.
