# Unknown and misspelled command-line options are silently ignored

**Status:** not started

## Description

`src/Whatinator.Cli/CommandLineOptions.cs`

`CommandLineOptions` only ever *searches* `args` for names it is asked about
(`:11-28`). No command validates that every token in `args` was actually
consumed, and there is no unknown-option path anywhere in the CLI.

### Silently ignored options

- `whatinator pipeline --no-mp3s` (or `--nomp3`, `--no_mp3`) -> `HasFlag(args, "--no-mp3")`
  is false (`PipelineCommand.cs:55`), MP3 encoding runs anyway, adding many
  minutes the user explicitly asked to skip.
- `whatinator rip --releaseinfo r.json --dsc 2` -> `--disc` never matches
  (`RipCommand.cs:56`), `discNumber` stays `null`. For a multi-disc release
  `ReleaseFolderNaming.ResolveDiscNumber` throws; for a single-disc release it
  silently rips as disc 1 with no complaint.
- `whatinator make-checksum --destination out/` -> `--dest` never matches
  (`MakeChecksumCommand.cs:43`), silently hashes the current directory.
- `whatinator list-device --device /dev/sr0` -> `CommandDispatcher.cs:38` calls
  `ListDeviceCommand.Run()` with **no arguments at all**; `rest` is discarded
  entirely.

### `GetValue` accepts the next flag as a value

`CommandLineOptions.cs:15` requires only `i + 1 < args.Length`; it never checks
whether `args[i + 1]` is itself an option.

- `whatinator make-checksum --dest` (value forgotten) -> `null` -> falls back to
  `"."` and hashes the current directory. Same shape for `--device` on
  `disc-info`/`toc`/`rip`/`pipeline`/`offset-find`, which quietly fall back to
  the config default.
- `whatinator rip --releaseinfo --keep-wav` -> `releaseInfoPath` becomes the
  literal string `"--keep-wav"`, producing `Failed to read --keep-wav: Could not
  find file ...` rather than "missing value for --releaseinfo".
- `whatinator pipeline --dest --no-mp3` -> `dest` becomes `"--no-mp3"` (a
  directory with that name is created at `PipelineCommand.cs:161`) **and**
  `HasFlag` still reports `--no-mp3` as set, because `:28` is a plain
  `args.Contains`. The token is consumed twice.

### Duplicated options take the first occurrence

`CommandLineOptions.cs:12-19` returns on first match, so
`whatinator rip --disc 1 --disc 2` rips disc 1 with no warning. Deterministic
but undocumented, and the later (usually intended) value is discarded.

## Acceptance Criteria

- [ ] Each command declares its known long/short option names.
- [ ] After parsing, any leading-`-` token not in that set and not consumed as a
      value produces `Unknown option: --foo` on stderr and a non-zero exit.
- [ ] `GetValue` treats a missing next token, or one beginning with `-`, as an
      error ("--dest requires a value") rather than as absence or as a value.
      Return a tri-state: absent / present-without-value / value.
- [ ] Consumed indices are tracked so `HasFlag` cannot match a token already
      taken as a value.
- [ ] Duplicate occurrences either error out or implement documented last-wins.
- [ ] `list-device` rejects arguments instead of discarding them.
- [ ] New tests covering: unknown option, option with missing value, option whose
      value is another flag, duplicated option, and `list-device` with extra args.
      `Whatinator.Cli` currently has no test project -- this is a good reason to
      add one, since `CommandLineOptions` is pure and needs no hardware.
