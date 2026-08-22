# Ctrl-C orphans subprocesses; cancellation is plumbed but never connected

**Status:** not started

## Description

Two separate defects that combine into one bad outcome.

### 1. The CLI never creates a cancellation token

`Whatinator.Cli` contains **zero** occurrences of `CancellationToken`,
`CancellationTokenSource`, `Console.CancelKeyPress`, or `PosixSignalRegistration`.
Meanwhile `Whatinator.Core` threads a `CancellationToken` through
`CdrdaoTocReader.ReadAsync`, `WhatinatorRipRunner.RipAsync`,
`PipelineRunner.RunDiscAsync`, `Mp3Packager.PackageAsync`,
`FlacEncoder.EncodeAsync`, `LameEncoder.EncodeAsync`, `OffsetFinder.FindAsync`,
and `AccurateRipClient.GetEntriesAsync`.

Every CLI call site passes `default`: `RipCommand.cs:91,108`,
`PipelineCommand.cs:97-109`, `TocCommand.cs:28`, `Mp3Command.cs:140-145`,
`OffsetFindCommand.cs:277`, `FlacCommand.cs:61-63`.

The entire cancellation path is plumbing that was built and never connected.
Ctrl-C is a hard process kill.

### 2. Cancelling would not kill the child process anyway

The same shape appears in six subprocess wrappers:

- `Rip/CdParanoiaTrackReader.cs:344-361` (`RunCdParanoiaAsync`) and `:473-493` (`sox`)
- `Toc/CdrdaoTocReader.cs:47-55`
- `Flac/FlacEncoder.cs:32-39`
- `Mp3/LameEncoder.cs:35-43`
- `Drive/CacheDefeatAnalyzer.cs:87-96`

```csharp
using var process = new Process { StartInfo = ... };
process.Start();
var relayTask = ...(cancellationToken);
await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);  // throws on cancel
await Task.WhenAll(relayTask, drainTask).ConfigureAwait(false);           // never reached
```

On cancellation `WaitForExitAsync` throws, the `using` disposes the `Process`
**object** (which does not terminate the child), and both relay tasks are
abandoned mid-`ReadAsync` and left unobserved.

## Failure scenario

User hits Ctrl-C 20 minutes into `whatinator pipeline`:

- `cd-paranoia` keeps running and holds `/dev/sr1` open; the next rip fails to
  open the drive with no obvious cause.
- Scratch files `whatinator-{guid}-test.wav` / `-copy.wav` (~50 MB each) stay in
  the destination directory -- `TryOnceAsync`'s `finally` at `:408-419` does run,
  but the still-running child re-creates and holds them.
- No rip log is written (`RipCommand.cs:117-148` never runs), so the output
  folder is left with a partially-written mix of good and truncated files and no
  record of what happened.

## Acceptance Criteria

- [ ] `Program.cs` creates a `CancellationTokenSource`, registers
      `Console.CancelKeyPress` (setting `e.Cancel = true` and calling
      `cts.Cancel()`), and threads the token through
      `CommandDispatcher.RunAsync` into every command's Core calls.
- [ ] Top-level `catch (OperationCanceledException)` prints `Cancelled.` and
      exits 130.
- [ ] Every subprocess wrapper kills its child on cancellation:
      `catch (OperationCanceledException) { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } throw; }`
      -- ideally implemented once in the shared subprocess runner proposed in the
      encoder-duplication backlog item, not six times.
- [ ] Scratch WAV files are cleaned up on cancellation.
- [ ] New tests: cancelling `RunCdParanoiaAsync` / `FlacEncoder.EncodeAsync`
      terminates the child process and removes scratch files. There is currently
      no cancellation test for any subprocess wrapper.
- [ ] Manual verification: Ctrl-C mid-rip, then confirm no `cd-paranoia` process
      remains (`pgrep cd-paranoia`) and `/dev/sr1` can be reopened immediately.
