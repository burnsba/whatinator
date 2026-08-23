# No top-level exception handler; a malformed config stack-traces every command

**Status:** done

## Description

`src/Whatinator.Cli/Program.cs` is eleven lines with no `try`/`catch`:

```csharp
var services = new ServiceCollection();
services.AddHttpClient();
using var serviceProvider = services.BuildServiceProvider();
var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
return await CommandDispatcher.RunAsync(args, httpClientFactory);
```

Meanwhile `ConfigLoader.Load` explicitly documents that it throws:

```csharp
/// <exception cref="JsonException">The config file exists but isn't valid JSON.</exception>
```

and the `JsonSerializer.Deserialize` call at `ConfigLoader.cs:38` is unguarded.
It is called unguarded by **every** device-aware command:
`DiscInfoCommand.cs:25`, `TocCommand.cs:21`, `MakeReleaseInfoCommand.cs:70`,
`RipCommand.cs:68`, `PipelineCommand.cs:52`, `OffsetFindCommand.cs:264`.

## Failure scenario

The README instructs users to hand-edit
`~/.config/whatinator/config.json` (to add `readOffsets` / `cacheDefeats`
entries). One trailing comma and **every** command -- including `toc`, which
needs nothing from config beyond a device default -- dies with:

```
Unhandled exception. System.Text.Json.JsonException: ',' is invalid after a value...
   at System.Text.Json.ThrowHelper.ReThrowWithPath(...)
   ...
```

The user's actual mistake (one character in a JSON file) is buried under a
managed stack trace, and the config file's path is never mentioned.

## Related unguarded paths

- `UnauthorizedAccessException` does **not** derive from `IOException`, so the
  `catch (Exception ex) when (ex is IOException or JsonException)` filters used
  throughout the CLI do not cover a read-only destination anywhere.
  Affected: `MakeReleaseInfoCommand.cs:48-50`, `IdTxtCommand.cs:35-37`,
  `PipelineCommand.cs:161-162`.
- `DllNotFoundException` from libdiscid -- see the separate LibDiscId backlog item.
- `TaskCanceledException` / `NotSupportedException` from MusicBrainz -- see the
  separate HTTP robustness backlog item.

## Acceptance Criteria

- [ ] `Program.cs` wraps `CommandDispatcher.RunAsync` in a handler that prints
      `ex.Message` to stderr (no stack trace) and returns a non-zero exit code.
      Consider an opt-in `--debug` / env var that re-enables the full trace.
- [ ] `ConfigLoader.Load` failures are reported with the **config file path**
      named in the message, so the user knows which file to fix.
- [ ] `UnauthorizedAccessException` added to the catch filters at the three write
      sites listed above.
- [ ] New tests: `ConfigLoader.Load` against a malformed JSON file produces a
      diagnosable error; `CommandDispatcher.RunAsync` returns a non-zero exit code
      rather than propagating.
- [ ] Manual verification: corrupt `~/.config/whatinator/config.json`, run
      `whatinator toc`, confirm a one-line actionable error and no stack trace.
