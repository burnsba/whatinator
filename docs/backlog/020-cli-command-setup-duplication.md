# Substantial duplicated setup across the CLI command files

**Status:** not started

## Description

Beyond the User-Agent constant, four blocks are copy-pasted across
`src/Whatinator.Cli/*Command.cs`:

1. **`--disc` parse** -- eleven identical lines at `RipCommand.cs:55-66`,
   `FlacCommand.cs:31-42`, `Mp3Command.cs:111-122`, including the identical error
   string `"--disc must be a number, got '{discArg}'."`.
2. **releaseinfo load + catch filter** -- `IdTxtCommand.cs:24-33`,
   `RipCommand.cs:44-53`, `FlacCommand.cs:44-53`, `Mp3Command.cs:124-133`,
   `PipelineCommand.cs:137-148`, `UpdateMetadataCommand.cs:147-156`. Six
   near-identical blocks -- and a seventh site that forgot it entirely
   (`make-releaseinfo`), which is the classic consequence.
3. **device resolution** --
   `CommandLineOptions.GetValue(args, "--device", "-d") ?? ConfigLoader.Load().Device`
   at `DiscInfoCommand.cs:25`, `TocCommand.cs:21`, `MakeReleaseInfoCommand.cs:70`,
   `RipCommand.cs:69`, `PipelineCommand.cs:53`, `OffsetFindCommand.cs:265`.
   Note the inconsistency: `disc-info`/`toc`/`make-releaseinfo` call
   `ConfigLoader.Load()` **inline inside the `??`**, re-reading and
   re-deserializing the config file, while `rip`/`pipeline`/`offset-find` hold a
   `config` local. In `pipeline` the config is loaded twice per run
   (`PipelineCommand.cs:52`, and again via `MakeReleaseInfoCommand.cs:70` on the
   lookup path).
4. **drive lookup + offset + environment** --
   `OpticalDriveLocator.Enumerate().FirstOrDefault(d => d.DevicePath == device)`
   then `GetReadOffset` then `RipEnvironmentResolver.Resolve` at
   `RipCommand.cs:72-74` and `PipelineCommand.cs:58-60`; the
   `Enumerate`+`FirstOrDefault` pair recurs at `OffsetFindCommand.cs:302`.

## Proposed fix

A `CommandContext` record built once per invocation -- config, device, resolved
drive, read offset, rip environment, user agent -- plus shared
`TryParseDiscNumber` and `TryLoadReleaseInfo` helpers.
`RipEnvironmentResolver.cs` already establishes this pattern for one slice of the
problem; extend it rather than inventing a second shape.

This is also the natural place to thread the `CancellationToken` (see the Ctrl-C
item) and the consolidated User-Agent.

## Acceptance Criteria

- [ ] `CommandContext` (or equivalent) built once per invocation and passed to
      commands; config read exactly once per run.
- [ ] Shared `TryParseDiscNumber` and `TryLoadReleaseInfo` helpers; the duplicated
      blocks deleted.
- [ ] `make-releaseinfo` picks up the load guard for free as a result.
- [ ] Behaviour unchanged: same error messages, same exit codes. Verify by running
      each command's failure paths before and after.
- [ ] New tests over the shared helpers -- these are pure and need no hardware,
      which makes them a good seed for the CLI test project the repo currently
      lacks.
