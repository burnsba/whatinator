# Rip log timestamps are UTC while console and MP3 log are local

**Status:** not started

## Description

Three different clocks in one run:

- `Rip/PipelineRunner.cs:80,83` and `Cli/RipCommand.cs:105,116` use
  `DateTimeOffset.UtcNow`.
- `Mp3/Mp3Packager.cs:81,84` uses `DateTimeOffset.Now` (local).
- `Rip/RipOutputTimestamp.cs:14` uses `DateTime.Now` (local) for the
  `yyyyMMdd-HHmmss: ` console prefix.

So `WhatinatorLogHeader`'s `logfile from {date}, {HH:mm}` line in the rip log
disagrees with the timestamps printed on the console **during that same rip**,
and with the MP3 log written minutes later from the same session.

For a user in a non-UTC timezone this makes the logs hard to correlate, and the
rip log's start/end times do not match when they actually sat at the machine.
EAC logs use local time.

## Acceptance Criteria

- [ ] One convention chosen -- local time, to match `RipOutputTimestamp`, the MP3
      log, and EAC's own convention.
- [ ] `PipelineRunner` and `RipCommand` switched to `DateTimeOffset.Now`.
- [ ] Comment recording the convention so it does not drift again.
- [ ] `WhatinatorEacLogTests` / `Mp3LogFileTests` assert against a fixed
      `DateTimeOffset` with a non-UTC offset, so a future regression to `UtcNow`
      fails a test rather than passing silently.
- [ ] Manual verification: rip log header time matches the console prefix times
      from the same run.
