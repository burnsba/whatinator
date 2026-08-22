# HTTP clients mutate an HttpClient they document as caller-owned

**Status:** not started

## Description

All four API clients set `BaseAddress` and call
`DefaultRequestHeaders.UserAgent.ParseAdd(...)` on an `HttpClient` their own XML
doc describes as "owned by the caller (typically resolved from a shared
`IHttpClientFactory`), not disposed by this class":

- `MusicBrainz/MusicBrainzClient.cs:68-70`
- `Discogs/DiscogsClient.cs:41-43`
- `CoverArt/CoverArtClient.cs:124-126`
- `AccurateRip/AccurateRipClient.cs:39-41`

Mutating shared state whose ownership you explicitly disclaim is a latent bug:

- `DefaultRequestHeaders.UserAgent.ParseAdd` **appends**. Constructing two
  clients over one `HttpClient` sends a doubled `User-Agent`.
- Assigning `BaseAddress` a second time throws `InvalidOperationException` once a
  request has been sent on that instance.

Today it happens to be safe: `Program.cs:6` calls bare `services.AddHttpClient()`
and each command calls `CreateClient("name")` once, and `CreateClient` returns a
**fresh** `HttpClient` per call (only the underlying handler is pooled). But
nothing enforces or documents that, and it is one refactor away from breaking --
for example, hoisting a client into a `CommandContext` (as proposed in the CLI
duplication item) and reusing it.

## Proposed fix

Configure the named clients once in `Program.cs` and reduce the constructors to
taking only the `HttpClient`:

```csharp
services.AddHttpClient("musicbrainz", c => {
    c.BaseAddress = new Uri("https://musicbrainz.org/ws/2/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
});
```

This lands the User-Agent consolidation in the same stroke -- do the two together.

## Acceptance Criteria

- [ ] Named clients configured once in `Program.cs`; base addresses and the
      User-Agent set there, not in the client constructors.
- [ ] Client constructors take only the `HttpClient` (plus their genuine
      collaborators such as `onRetry` / `delayAsync`) and mutate nothing on it.
- [ ] Client doc comments updated to state that configuration is the caller's
      responsibility.
- [ ] Existing client tests updated to configure `BaseAddress` on the stub client
      rather than relying on the constructor to do it.
- [ ] New test: constructing two clients over the same `HttpClient` does not
      produce a doubled `User-Agent` -- or, if the design forbids sharing, the
      constructor documents and enforces that.
