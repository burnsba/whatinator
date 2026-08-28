# Network lookups: heartbeat message during a single slow/stalled attempt

**Status:** not started

## Description

Follow-up to `docs/backlog-completed/056-metadata-lookup-progress-messages.md`.
That item made the CLI print a status line *before* each MusicBrainz/Discogs
network call, and print a status line *between* retries when
`MusicBrainzClient`'s backoff kicks in (`ReportMusicBrainzRetry`). It does not
cover the gap observed live afterward: a single attempt that is simply very
slow (MusicBrainz took, in one observed run, "over 3 minutes" to answer a
disc-ID lookup that eventually succeeded -- no retry ever fired, because it
never failed, it was just slow). During that window the CLI printed nothing
after `Looking up disc ID <id> on MusicBrainz...` and looked identical to a
hang, even though the process was alive and the socket connected (confirmed
via `ps`/`ss` and by reproducing the same multi-minute latency directly with
`curl` against the same endpoint).

`MusicBrainzClient.GetAsync<T>` (`src/Whatinator.Core/MusicBrainz/MusicBrainzClient.cs`)
relies on `HttpClient.Timeout` (100s default, not overridden anywhere) as the
only signal a slow request is in trouble, and that only produces visible
output (`_onRetry`) *after* the timeout fires and a retry begins -- there's
nothing that fires *while* a single attempt is still in flight. `DiscogsClient`
(`src/Whatinator.Core/Discogs/DiscogsClient.cs`) has no retry/timeout
instrumentation at all.

Per root `CLAUDE.md`'s Core design rules ("No console I/O in Core" --
`Whatinator.Core/CLAUDE.md`), any new console output belongs in
`Whatinator.Cli`, following the same pattern `onRetry` already uses to let
`MusicBrainzClient` report progress without doing I/O itself.

## Possible approach

Race each network call against a periodic timer (e.g. every 15-30s) that
prints something like `Still waiting on MusicBrainz...` (or `...Discogs...`)
while the underlying request hasn't completed yet, cancelling the timer as
soon as the call returns or throws. This is the same shape of problem
`StallMonitor` already solves for `cd-paranoia` (see root `CLAUDE.md` §
"External tools misbehave in specific, known ways" and
`src/Whatinator.Core/Rip/`) -- a "no forward progress for N seconds" watchdog
-- though here there's no incremental progress to watch, just elapsed wall
time, so a bare periodic tick is probably sufficient rather than anything
resembling `StallMonitor`'s stall detection.

Needs to work for the three call sites `056` touched: the initial MusicBrainz
disc-ID lookup, the full-release fetch after a picker choice/manual override,
and the Discogs barcode search, and ideally the manual-URL-override fetches
too (`MakeReleaseInfoCommand.cs`).

## Acceptance Criteria

- [ ] A single slow (but not yet failing) network attempt to MusicBrainz or
      Discogs produces periodic console output so the CLI doesn't look hung
      while waiting.
- [ ] The heartbeat stops as soon as the request completes (success or
      failure) -- no output after the fact, no leaked timer/background task.
- [ ] No console I/O added to `Whatinator.Core` -- consistent with `056`,
      any new output lives in `Whatinator.Cli` (or the instrumentation hook
      is exposed the same way `MusicBrainzClient`'s `onRetry` is, if a hook
      turns out to be the cleaner shape here too).
- [ ] Tests: whatever is unit-testable without a real network call (e.g. the
      timer/cancellation wiring against a fake delay, following the pattern
      `MusicBrainzClientTests` already uses to avoid real sleeps).
