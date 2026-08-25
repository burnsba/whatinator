# An empty libdiscid error string yields an exception with an empty message

**Status:** done

## Description

`Marshal.PtrToStringUTF8` returns `""` -- **not** `null` -- for a non-NULL
pointer to a zero-length string. The `?? fallback` idiom therefore only fires on
`IntPtr.Zero`.

Affected sites in `src/Whatinator.LibDiscId/DiscReader.cs`:

- `:36-37` -- the read-failure path
- `:83-84` -- `RequireString`
- `:67` -- `GetDefaultDevice`
- `:75` -- `GetNativeVersion`

## Failure scenario

libdiscid fails to read the disc but leaves its error buffer empty.
`DiscInfoCommand.cs:35` then prints:

```
Failed to read disc from /dev/sr1:
```

with nothing after the colon. The user learns a read failed but gets zero
diagnostic content -- no way to tell whether the drive is empty, busy, or
missing. This is precisely the "failed read indistinguishable from empty result"
case the wrapper's error handling exists to prevent.

Note `DiscReaderTests.cs:27` asserts
`!string.IsNullOrWhiteSpace(exception.Message)` -- the test *intends* this
guarantee, but the implementation does not provide it, and the test passes only
because the native library happens to populate the buffer in the case exercised.

## Acceptance Criteria

- [ ] All four sites use a null-**or-whitespace** check rather than `??`:
      `var m = Marshal.PtrToStringUTF8(ptr); if (string.IsNullOrWhiteSpace(m)) m = fallback;`
- [ ] The fallback message names the device and the operation attempted.
- [ ] New test exercising the empty-string path directly (a small helper taking
      the marshalled string rather than the pointer makes this testable without
      native code).
