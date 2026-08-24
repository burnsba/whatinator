# disc-info --ask exits 0 when no selection is made

**Status:** done

## Description

`src/Whatinator.Cli/DiscInfoCommand.cs:64-68`: when the picker returns null it
writes `"No selection made."` to **stderr** and `return 0`.

`src/Whatinator.Cli/MakeReleaseInfoCommand.cs:101-105` reaches the identical
condition and returns `null`, which becomes exit **1** at `:41-43`.

Two sibling commands hit the same state and disagree about the exit code.

## Failure scenario

A script runs:

```
whatinator disc-info --device /dev/sr1 --ask < /dev/null
```

`ConsolePicker`'s `Console.ReadLine()` returns `null` at EOF
(`ConsolePicker.cs:36-42`), the picker returns `null`, and the process exits **0**
having printed no release info. The calling script treats it as success.

## Acceptance Criteria

- [ ] `DiscInfoCommand.cs:67` returns 1, matching `make-releaseinfo`.
- [ ] Exit-code behaviour for "no selection made" documented in the README's
      `disc-info` entry.
- [ ] Verified alongside the non-TTY picker item -- ideally a non-interactive
      invocation reports "cannot prompt: stdin is not a terminal" rather than
      silently reading EOF.
