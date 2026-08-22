# Missing libdiscid produces a raw stack trace on first run

**Status:** not started

## Description

`src/Whatinator.LibDiscId/NativeMethods.cs:25` hardcodes the SONAME
`libdiscid.so.0`. The first native call throws `DllNotFoundException` if the
dynamic linker cannot resolve it.

`DiscReader.Read` catches nothing (`DiscReader.cs:23-60`), and both call sites
catch only `DiscIdException` (`DiscInfoCommand.cs:33`,
`MakeReleaseInfoCommand.cs:77`).

## Failure scenario

A fresh Debian box with the .NET SDK installed but without `libdiscid0`.
`whatinator disc-info` prints:

```
Unhandled exception. System.DllNotFoundException: Unable to load shared library
'libdiscid.so.0' or one of its dependencies...
   at Whatinator.LibDiscId.NativeMethods.discid_new()
   ...
```

plus a full managed stack trace, instead of "libdiscid is not installed -- run
`sudo apt install libdiscid0`". Since libdiscid is a hard requirement, this is
the **expected first-run failure for every new user**, and it is the ugliest
possible one.

The same applies on macOS (`libdiscid.0.dylib`) and Windows (`discid.dll`),
where the hardcoded name can never resolve at all.

## Related

`DiscReader`'s `?? fallback` idiom does not fire on an **empty** native error
string -- see the separate backlog item -- so even the handled path can produce a
message with nothing after the colon.

## Acceptance Criteria

- [ ] `DllNotFoundException` caught inside `DiscReader` and rethrown as
      `DiscIdException` with an actionable install hint naming the Debian package.
- [ ] Optionally register a `NativeLibrary.SetDllImportResolver` trying
      `libdiscid.so.0`, `libdiscid.so`, `libdiscid.0.dylib`, `discid` in turn, so
      the wrapper degrades usefully on other platforms.
- [ ] `[SupportedOSPlatform("linux")]` applied if Linux-only is the intended
      scope, so the constraint is declared rather than implied.
- [ ] New test: the exception type surfaced for an unresolvable library is
      `DiscIdException` with a non-empty, actionable message.
- [ ] Manual verification on a machine (or container) without `libdiscid0`:
      `whatinator disc-info` prints one actionable line and exits non-zero.
