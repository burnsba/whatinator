# CLAUDE.md -- Whatinator.LibDiscId

A hand-written P/Invoke wrapper over the native **libdiscid** C library. Its
entire job is: given a device path, return the disc's MusicBrainz disc ID and
TOC. Nothing else in this project talks to native code.

Read the root [`CLAUDE.md`](../../CLAUDE.md) first. This project has no
dependency on `Whatinator.Core` -- the arrow points the other way.

## The native dependency

- **Library:** `libdiscid` (MusicBrainz).
- **Bound SONAME:** `libdiscid.so.0`, hardcoded as `NativeMethods.LibraryName`.
  Resolved by the normal dynamic-linker SONAME lookup -- no absolute path, no
  `DllImportSearchPath`, no custom resolver.
- **Developed against:** `libdiscid.so.0.7.0`.
- **Install (Debian/Ubuntu):** `sudo apt install libdiscid0`. That's the
  *runtime* package, which is all that's needed. `libdiscid-dev` is **not**
  installed on the dev machine and is not required. Fedora/Arch: `libdiscid`.
- **Verify:** `ldconfig -p | grep libdiscid` should list `libdiscid.so.0`.
- **There is no local `discid.h`.** Signatures came from the stable, long-
  unchanged public C ABI, cross-checked with `nm -D` against the installed
  shared object. `nm -D` is the authority when adding a binding.
- **Linux-only as written.** The name `libdiscid.so.0` cannot resolve on macOS
  (`libdiscid.0.dylib`) or Windows (`discid.dll`). If the library isn't
  installed, the first native call throws `DllNotFoundException`, which this
  wrapper does **not** currently translate into a `DiscIdException` -- so it
  surfaces to the user as a raw stack trace. Worth fixing.

## Key types

| Type | Role |
| --- | --- |
| `DiscReader` | The only public entry point. `static`. `Read(device, features)` -> `Disc`; `GetDefaultDevice()`; `GetNativeVersion()`. |
| `Disc` | `sealed record`. `Id`, `FreedbId`, `SubmissionUrl`, `TocString`, `FirstTrack`, `LastTrack`, `Sectors`, `Tracks`. Pure managed data -- holds no native state. |
| `Track` | `sealed record` of `Number`/`OffsetSectors`/`LengthSectors`, plus computed `Offset`/`Duration` `TimeSpan`s at 75 sectors per second. |
| `DiscIdFeatures` | `[Flags] uint` mirroring `discid_feature_t`. `None = 0`, `Mcn = 1<<1`, `Isrc = 1<<2`. |
| `DiscIdException` | The only exception type callers are expected to catch. |
| `DiscIdSafeHandle` | `internal`. `SafeHandle` over `DiscId*`. Never leaves the assembly. |
| `NativeMethods` | `internal static partial`. Raw `[LibraryImport]` declarations only. |

`DISCID_FEATURE_READ` (`1 << 0`) is deliberately **absent** from
`DiscIdFeatures` -- `discid_read_sparse` implies it.

## The performance fact that shapes everything

A TOC-only read (`DiscIdFeatures.None`) takes about a second. `Mcn` or `Isrc`
forces libdiscid into a Q-subchannel pass across the **entire disc**, which
takes minutes. `None` is the default and should stay the default.

**Caveat, and a live trap:** `Mcn` and `Isrc` are accepted and forwarded to
native, but `discid_get_mcn` and `discid_get_track_isrc` are **not bound**, and
neither `Disc` nor `Track` has anywhere to put the values. Passing either flag
today buys a multi-minute read and returns a result identical to `None`. Nothing
in-tree passes them, so it's a trap rather than an active bug -- but either bind
the two getters and surface the data, or delete the flags. Don't leave them
accepted-but-inert.

(whatinator does get ISRC and the disc catalog number today -- from `cdrdao`,
via `Whatinator.Core.Toc`, not from here.)

## Interop conventions

These are the project's rules. Follow them exactly; several exist to avoid a
specific, real bug class.

1. **`[LibraryImport]`, never `[DllImport]`.** Source-generated marshalling.
   Requires the containing type to be `partial`, and requires
   `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in the csproj because the
   generator emits unsafe pointer code. That's the only reason it's on.
2. **Default calling convention.** libdiscid is plain cdecl on Linux; nothing
   is declared explicitly.
3. **`SetLastError` is not used, on purpose.** libdiscid reports failure via
   `discid_get_error_msg`, not `errno`. Adding it would cost a capture per call
   for nothing.
4. **A native `char*` return is declared as `IntPtr`, never as `string`**, and
   decoded at the call site in `DiscReader` with `Marshal.PtrToStringUTF8`.
   libdiscid returns UTF-8; letting the default marshaller choose would risk
   ANSI. It also keeps ownership explicit -- **these pointers are owned by the
   `DiscId` instance and must never be freed.**
5. **String *parameters* use `StringMarshalling = StringMarshalling.Utf8`** on
   the attribute. Only `discid_read_sparse`'s `device` today.
6. **Every native function taking a `DiscId*` takes `DiscIdSafeHandle`**, so the
   marshaller does the AddRef/Release and the handle can't be collected
   mid-call. **The one exception is `discid_free`, which takes a raw `IntPtr`**
   -- it's called from `ReleaseHandle`, where the handle is already being torn
   down, and a `SafeHandle` parameter there would deadlock against its own
   finalization.
7. **Native strings are copied eagerly, inside the handle's lifetime.** Every
   `PtrToStringUTF8` call happens before the `using var handle` scope closes,
   so nothing in `Disc`/`Track` points at native memory after `Read` returns.
   **Never add a lazily-evaluated property that defers a native read** -- the
   handle will be gone.
8. **Error convention:** `discid_read_sparse` returns `1` on success, `0` on
   failure. On `0`, read `discid_get_error_msg(handle)` and wrap it in a
   `DiscIdException`.
9. **Reentrancy:** `DiscReader` holds no static mutable state and each `Read`
   allocates its own handle, so concurrent reads of different devices are safe.
   `GetDefaultDevice` is the exception -- libdiscid returns a shared static
   buffer there, so it is not thread-safe.

### A sharp edge in the current code

`Marshal.PtrToStringUTF8` returns `""` -- not `null` -- for a non-NULL pointer
to a zero-length string. `DiscReader`'s `?? fallback` idiom therefore only fires
on `IntPtr.Zero`. If libdiscid fails but leaves its error buffer empty, the
resulting `DiscIdException` has an empty `Message`, and the CLI prints
`Failed to read /dev/sr1: ` with nothing after the colon. Use a null-**or
whitespace** check when touching these paths.

## Adding a new native binding

1. Confirm the symbol exists:
   `nm -D /usr/lib/x86_64-linux-gnu/libdiscid.so.0 | grep discid_`.
   There's no local header, so this is the authority.
2. Add a `[LibraryImport(LibraryName)] internal static partial` declaration to
   `NativeMethods.cs`, following the conventions above.
3. Write the XML doc comment. `GenerateDocumentationFile` plus the analyzer
   settings make CS1591 fire, so an undocumented member won't build. For a
   string return, state explicitly that the pointer is owned by the `DiscId`
   and must not be freed -- every existing declaration does.
4. Surface it through `DiscReader`, decoding strings **inside** the
   `using var handle` block (use `RequireString` for a mandatory value).
5. If it's per-track or per-disc data, add it to the `Track`/`Disc` record
   rather than exposing a new call. The records are the public contract.
6. Add whatever test is possible without hardware (see below).

**Currently bound:** `discid_new`, `discid_free`, `discid_read_sparse`,
`discid_get_id`, `discid_get_freedb_id`, `discid_get_submission_url`,
`discid_get_toc_string`, `discid_get_first_track_num`,
`discid_get_last_track_num`, `discid_get_sectors`, `discid_get_track_offset`,
`discid_get_track_length`, `discid_get_error_msg`, `discid_get_default_device`,
`discid_get_version_string`.

**Not bound:** `discid_get_mcn`, `discid_get_track_isrc` (see above),
`discid_put`, `discid_read`.

## Testing constraints

**No disc, no drive, no native library needed:**
- Pure managed logic -- `Track.Offset`/`Track.Duration` sector-to-`TimeSpan`
  conversion, `DiscIdException` constructors.
- Argument validation that short-circuits before native code: `Read` with a
  null/empty/whitespace device throws `ArgumentException` before `discid_new`.

**Needs libdiscid installed, but no disc:**
- `GetNativeVersion()`, `GetDefaultDevice()`.
- `Read("/dev/nonexistent")` -- note this *does* cross into native code and
  fails at `open()`; it does not short-circuit.
- On a machine without `libdiscid0` these fail with `DllNotFoundException`
  rather than skipping. `dotnet test` is not hermetic here.

**Cannot be tested automatically at all:**
- The real device-read path: a populated `Disc`, a correct disc ID, real track
  offsets. Verified by hand against real hardware. `TrackTests` preserves one
  known-good real-disc data point (Annie Lennox - *Diva*, track 1 = 22030
  sectors = 4:53.73, cross-checked against MusicBrainz) as a regression anchor
  for the conversion math. Keep that kind of anchor when adding TOC-derived
  computations.
- Because `DiscReader` is a **static class with no interface**, the CLI's
  `disc-info` and `make-releaseinfo` disc-read branches can't be faked either.
  Introducing an `IDiscReader` (with `DiscReader` as a thin static shim) is what
  would unlock testing them -- `Disc`/`Track` are already pure records, so
  nothing else would need to change.
