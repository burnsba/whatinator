# Unknown-command help is written to stdout while the error goes to stderr

**Status:** not started

## Description

`src/Whatinator.Cli/CommandDispatcher.cs:63-67` writes
`Unknown command: {command}` to **stderr**, then calls `HelpFormatter.Print()`,
which writes every line to **stdout** (`HelpFormatter.cs:29-48`).

So:

- `whatinator bogus 2>/dev/null` emits the full help text on stdout and exits 1 --
  looking like success with output.
- `whatinator bogus | head` shows help with no visible error at all, because the
  error went to the discarded stream.

On the error path, everything should go to stderr.

## Acceptance Criteria

- [ ] `HelpFormatter.Print` parameterised with a `TextWriter` (defaulting to
      `Console.Out`).
- [ ] The unknown-command path passes `Console.Error`.
- [ ] The explicit `help` / `--help` / `-h` path continues to write to stdout --
      requested help is output, not an error.
- [ ] New test: unknown command writes nothing to stdout and exits non-zero.
