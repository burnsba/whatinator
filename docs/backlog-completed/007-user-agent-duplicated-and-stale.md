# HTTP User-Agent is duplicated six times and reports a stale version

**Status:** done

## Description

The identical literal

```
"whatinator/0.1 ( bethany.whatinator@burnsba.net )"
```

appears as a `private const string UserAgent` in six CLI command files:

| File | Line |
| --- | --- |
| `src/Whatinator.Cli/DiscInfoCommand.cs` | 12 |
| `src/Whatinator.Cli/FlacCommand.cs` | 13 |
| `src/Whatinator.Cli/MakeReleaseInfoCommand.cs` | 17 |
| `src/Whatinator.Cli/OffsetFindCommand.cs` | 12 |
| `src/Whatinator.Cli/PipelineCommand.cs` | 26 |
| `src/Whatinator.Cli/RipCommand.cs` | 29 |

Consumed by `MusicBrainzClient`, `DiscogsClient`, `CoverArtClient`, and
`AccurateRipClient`, each of which takes it as a constructor parameter.

Two problems, and the second is the serious one.

**Six copies to keep in sync.** Ordinary duplication.

**The version is wrong.** It says `0.1` while `Directory.Build.props` says
`<Version>1.0.3</Version>`. The repo already has the correct mechanism:
`Whatinator.Core.WhatinatorVersion.Current` reads the version from assembly
metadata, and `Directory.Build.props` carries a comment stating the version is a
single source of truth "read at runtime via assembly metadata rather than
duplicated as a literal anywhere". `CommandDispatcher.cs:35` and
`HelpFormatter.cs:30` both use it correctly. The `User-Agent` is the one place
that ignores it, so every whatinator install worldwide has been identifying
itself as version 0.1 for three releases.

This matters beyond tidiness: MusicBrainz treats the `User-Agent` as its
rate-limiting and abuse-triage key -- `MusicBrainzClient.cs:28-33`'s own
constructor doc says requests with a generic or missing User-Agent may be rate
limited. If a bug in a specific release triggers throttling, the server operator
cannot distinguish versions, and whatinator cannot be selectively unblocked.

## Proposed fix

Add a `UserAgent` property to `WhatinatorConfig` (defaulting to a computed
value), plus a single constant in `Whatinator.Core` that builds it:

```csharp
public static string Default => $"whatinator/{WhatinatorVersion.Current} ( {ContactEmail} )";
```

Then delete all six constants. Making it a config key also lets a user
substitute their own contact address, which is the polite thing for a tool that
identifies itself to third-party APIs on the user's behalf.

Best done together with the `HttpClient` configuration item -- configuring the
named clients once in `Program.cs` lands both fixes in one change.

## Acceptance Criteria

- [ ] Single source for the User-Agent string; all six `private const string UserAgent`
      declarations deleted.
- [ ] Version segment derived from `WhatinatorVersion.Current`, never a literal.
- [ ] `UserAgent` exposed as an optional `WhatinatorConfig` key so a user can
      supply their own contact address; documented in the README config table.
- [ ] New test: the default User-Agent contains the current assembly version and
      does not contain `0.1`.
- [ ] Manual verification: confirm the header actually sent matches (a request
      against MusicBrainz with logging, or a local echo server).
