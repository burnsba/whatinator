# ConsolePicker prompts unconditionally on non-TTY stdin and writes to stdout

**Status:** not started

## Description

`src/Whatinator.Cli/ConsolePicker.cs:18-51`

`PromptForSelection` never consults `Console.IsInputRedirected`, and writes the
candidate list and the `Select [1-N]:` prompt to **stdout** (`:21,24,35`),
interleaving them with the command's real output.

## Failure scenarios

- `whatinator disc-info --ask > out.txt` on an ambiguous disc: `out.txt` is
  polluted with the picker menu and the `Select [1-3]:` prompt, and -- because
  stdin is still the terminal -- the user sees no prompt on screen while the
  program appears to hang.
- Piped input that is not a valid number (`echo hi | whatinator make-releaseinfo`)
  loops through `"Invalid selection, try again."` (`:49`) once per input line
  before EOF finally breaks it. Noisy, though it does terminate.
- The Discogs picker uses `allowSkip: true` (`MakeReleaseInfoCommand.cs:160-164`),
  so an EOF-`null` and a deliberate user skip are **indistinguishable** at `:167`.
  A non-interactive run silently produces an unenriched release rather than
  reporting that it could not ask.

## Related prompt sites

- `PipelineCommand.cs:82-88` -- the multi-disc swap prompt. This one at least
  handles `null` correctly.
- `UpdateMetadataCommand.cs:163-169` -- the y/N confirmation, which correctly
  treats `null` as "no".

## Acceptance Criteria

- [ ] `ConsolePicker` checks `Console.IsInputRedirected` at entry and either fails
      fast with a clear message ("multiple matches; rerun interactively or pass
      --releaseinfo") or auto-selects per a documented rule.
- [ ] The menu and prompt are written to **stderr** so stdout stays clean and
      pipeable.
- [ ] EOF is distinguished from a deliberate skip via an explicit result type
      rather than overloading `null`, so the Discogs path can report "could not
      prompt" separately from "user skipped".
- [ ] The same non-TTY guard applied to `PipelineCommand`'s disc-swap prompt --
      an unattended multi-disc pipeline should fail with a clear message rather
      than silently proceeding.
- [ ] New tests over the picker with a redirected/EOF stdin: no infinite prompt
      loop, correct result type, nothing written to stdout.
- [ ] Manual verification: `whatinator disc-info --ask > out.txt` produces a clean
      `out.txt` and a visible prompt on the terminal.
