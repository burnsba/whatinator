# Working CLI aliases are documented nowhere; -d collides with --dest

**Status:** done

## Description

All thirteen subcommands appear consistently across `CommandDispatcher`,
`HelpContent`, and the README. The gaps are all in **option aliases**:

| Surface | `CommandDispatcher` accepts | README documents | `HelpContent` prints |
| --- | --- | --- | --- |
| `-h` / `--help` | yes (`CommandDispatcher.cs:28-30`) | yes | **no** -- only `help` (`HelpContent.cs:101`) |
| `-v` | yes (`CommandDispatcher.cs:34`) | yes | **no** -- only `--version` (`HelpContent.cs:102`) |
| `-d` (short for `--device`) | yes, in six commands | **no** | **no** |

`-d` is accepted by `DiscInfoCommand.cs:25`, `TocCommand.cs:21`,
`RipCommand.cs:69`, `PipelineCommand.cs:53`, `OffsetFindCommand.cs:265`,
`MakeReleaseInfoCommand.cs:70`.

## Why `-d` is the significant one

It is a working, six-command-wide short option that is invisible in both
documentation surfaces. It is also a readability trap: `-d` means `--device`,
while the adjacent `--dest` -- which appears in nine commands and is the option a
user is far more likely to be reaching for -- has **no** short form. So
`-d out/` silently sets the *device* to `out/` rather than the destination.

Full alias inventory (from `GetValue` call sites): `--dest` x9, `--releaseinfo`
x7, `--disc` x3, `--device`/`-d` x6, `--source` x2, `--multi` x1. `-d` is the
only short alias in the entire CLI.

## Project convention

`init.md` states that new console commands must be documented in both the README
and `--help`. These two surfaces drifting apart is exactly the failure mode that
rule exists to prevent, so fix them together.

## Acceptance Criteria

- [ ] Decision recorded on `-d`: either drop it entirely (given the `--dest`
      collision hazard) or keep it and document it.
- [ ] If kept: `-d` added to the README option tables and the relevant
      `HelpOption` entries, with the `--dest` distinction called out explicitly.
- [ ] `-h` and `-v` aliases added to the `Info` section of `HelpContent.cs:100-103`.
- [ ] The three surfaces re-verified as consistent (a scripted cross-check of
      dispatcher cases vs `HelpContent` entries vs README table rows would make
      this a permanent guarantee rather than a one-time audit).
