# HTTP clients lack cancellation and lose two failure modes

**Status:** not started

## Description

### No cancellation on any HTTP path

`IMusicBrainzClient.LookupByDiscIdAsync` / `GetReleaseAsync`,
`IDiscogsClient.SearchByBarcodeAsync`, and
`ICoverArtClient.TryDownloadFrontCoverAsync` take **no** `CancellationToken` --
unlike `IAccurateRipClient`, which does.

This is worse than it sounds because `MusicBrainzClient.GetAsync`
(`MusicBrainz/MusicBrainzClient.cs:114-138`) retries up to 10 times with
exponential backoff capped at 3m10s. A fully backed-off failure sequence runs
about **13 minutes**, and `await _delayAsync(delay)` at `:127` has no token to
observe. There is no way to interrupt it -- and per the Ctrl-C backlog item,
Ctrl-C will not help either.

### Two failure modes escape unwrapped

`MusicBrainzClient.GetAsync:118-137` catches only `HttpRequestException` and
`JsonException`.

- A **client-side timeout** (the default 100-second `HttpClient.Timeout`) throws
  `TaskCanceledException` in .NET 5+, not `HttpRequestException`. It is neither
  retried -- despite the doc at `:106-108` claiming timeouts are transient and
  retried -- nor wrapped. It escapes past `MakeReleaseInfoCommand.cs:116`'s
  `catch (MusicBrainzException)` and crashes the CLI with a stack trace.
- A response with an unexpected `Content-Type` (MusicBrainz serving an HTML
  error or maintenance page, which does happen) makes `GetFromJsonAsync` throw
  `NotSupportedException` -- likewise unwrapped and uncaught.

## Acceptance Criteria

- [ ] `CancellationToken` parameter added to `IMusicBrainzClient`,
      `IDiscogsClient`, and `ICoverArtClient` and their implementations, passed
      through to `GetFromJsonAsync` / `GetAsync` / `Task.Delay`.
- [ ] Threaded from the CLI (depends on the Ctrl-C backlog item).
- [ ] `TaskCanceledException` whose token is **not** the caller's is treated as a
      transient timeout: retried per the existing policy, and wrapped in
      `MusicBrainzException` if retries are exhausted.
- [ ] `NotSupportedException` wrapped in `MusicBrainzException` with a message
      naming the unexpected content type.
- [ ] The doc comment at `MusicBrainzClient.cs:106-108` corrected so it matches
      the code's actual retry behaviour.
- [ ] New tests using `StubHttpMessageHandler`: a timeout is retried then wrapped;
      an HTML response is wrapped, not thrown raw; a cancelled token aborts the
      backoff delay promptly rather than waiting out the full interval.
