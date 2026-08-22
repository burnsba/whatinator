# discid_get_default_device returns a shared static buffer

**Status:** not started

## Description

`src/Whatinator.LibDiscId/NativeMethods.cs:112-113`, used at `DiscReader.cs:67`.

In libdiscid's Linux backend this returns a pointer to a `static` buffer, not
per-instance storage, so it is not thread-safe against concurrent calls.

`DiscReader` is otherwise fully reentrant -- no static mutable state, and each
`Read` allocates its own handle -- which makes this the single exception to that
property, and therefore the one worth writing down.

Not reachable today: the CLI is single-threaded, and both `GetDefaultDevice` and
`GetNativeVersion` have **no callers outside the test project** (the CLI resolves
the device from config instead, `DiscInfoCommand.cs:25`).

## Acceptance Criteria

- [ ] The constraint documented in `GetDefaultDevice`'s XML doc comment -- stating
      that libdiscid returns a shared static buffer and the method is not
      thread-safe.
- [ ] Optionally wrap the call in a `lock` so the documented constraint is also
      enforced (cheap; called at most once per run).
- [ ] `DiscReader`'s type-level doc comment states that the class is otherwise
      reentrant, with this as the named exception -- so the guarantee is explicit
      rather than incidental.
