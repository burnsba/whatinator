# CoverArtProcessor can hang forever waiting on ImageMagick

**Status:** not started

## Description

`src/Whatinator.Core/CoverArt/CoverArtProcessor.cs`

`BuildStartInfo` sets both `RedirectStandardOutput = true` and
`RedirectStandardError = true`, and `ProcessAsync` then **never reads either
stream** before awaiting exit:

```csharp
using var process = Process.Start(startInfo);
if (process is null) { return original; }
await process.WaitForExitAsync().ConfigureAwait(false);
```

When a child process's redirected pipe buffer fills and nothing is draining it,
the child blocks on write and the parent blocks forever on exit. This is the
classic .NET subprocess deadlock.

`magick` emitting more than one pipe buffer of warnings is entirely plausible on
a malformed image or an unusual colour profile -- exactly the kind of thing that
arrives from the Cover Art Archive. The result is whatinator hanging
indefinitely mid-packaging, after the audio has already been moved into place.

Note `WaitForExitAsync()` here also takes no `CancellationToken`, so even once
cancellation is wired up this call would not observe it.

Every other subprocess wrapper in the codebase drains correctly (via
`ProcessOutputRelay` or explicit concurrent reads) -- this one is the exception.

## Related, lower probability

`src/Whatinator.Core/SystemInfo.cs:265-269` reads the two streams
**sequentially**:

```csharp
var output = wanted.ReadToEnd();
other.ReadToEnd();
process.WaitForExit();
```

Same deadlock class if the un-read stream fills first. Low probability given
version banners are small -- but `GetCdrdaoVersion` deliberately runs bare
`cdrdao` to capture its **entire usage text**, which is the largest output of the
five probes and the one most likely to grow in a future cdrdao release.

## Acceptance Criteria

- [ ] `CoverArtProcessor.ProcessAsync` drains stdout and stderr concurrently
      (to `Stream.Null` or via `ReadToEndAsync`) while awaiting exit.
- [ ] `WaitForExitAsync` given a `CancellationToken` (and `ProcessAsync` given
      one to pass down).
- [ ] `SystemInfo.RunCommand` reads both streams concurrently rather than
      sequentially.
- [ ] New test: a fake process producing more output than a pipe buffer
      (>64 KB on Linux) completes rather than hanging. A shell one-liner such as
      `yes | head -c 200000` invoked through the same code path is sufficient and
      needs no ImageMagick.
- [ ] Existing best-effort behaviour preserved: a failure still returns the
      original image unchanged rather than losing cover art.
