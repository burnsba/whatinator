# Reported extraction speed is roughly half the real speed

**Status:** not started

## Description

`src/Whatinator.Core/Rip/CdParanoiaTrackReader.cs:86-95` starts the stopwatch
**before** `RetryAsync`:

```csharp
var stopwatch = Stopwatch.StartNew();
var (success, attempts) = await RetryAsync(options.MaxRetries, async ct => { ... }, cancellationToken);
stopwatch.Stop();
```

So `CdParanoiaTrackResult.ElapsedTime` spans the test read **plus** the copy read
**plus** the `sox` peak analysis **plus** any retries.

`WhatinatorEacLog.FormatSpeed` (`:337-348`) then divides the track's audio
duration by that total. A drive reading at 16x therefore logs roughly `8.0 X`,
and a track that needed three retries logs about `2.7 X`.

The EAC log field this mirrors reports the drive's read speed, not the
wall-clock cost of the whole verify cycle, so the value is misleading to anyone
comparing logs against EAC output or against the drive's rated speed.

## Acceptance Criteria

- [ ] Decide which the field should mean: single-read drive speed (EAC parity) or
      total wall-clock cost per track.
- [ ] If drive speed: time a single `cd-paranoia` invocation and report that,
      leaving retries/`sox` out of the measurement.
- [ ] If wall-clock: relabel the log field so it is not mistaken for drive speed.
- [ ] Comment recording the choice and why.
- [ ] New test over `FormatSpeed` pinning the expected output for a known
      duration/elapsed pair.
- [ ] Manual verification: logged speed is plausible against the drive's rated
      speed for a clean rip.
