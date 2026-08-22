# DiscIdFeatures.Mcn and .Isrc are accepted, cost minutes, and return nothing

**Status:** not started

## Description

`src/Whatinator.LibDiscId/DiscIdFeatures.cs:21,24` publicly exposes `Mcn` and
`Isrc`, and `DiscReader.Read` forwards them to native at `DiscReader.cs:33`.

But `NativeMethods.cs` declares **no** `discid_get_mcn` and **no**
`discid_get_track_isrc` binding, and neither `Disc` nor `Track` has any field to
hold the values (`Disc.cs:12-20`, `Track.cs:7`).

Bound functions, for reference: `discid_new`, `discid_free`,
`discid_read_sparse`, `discid_get_id`, `discid_get_freedb_id`,
`discid_get_submission_url`, `discid_get_toc_string`,
`discid_get_first_track_num`, `discid_get_last_track_num`, `discid_get_sectors`,
`discid_get_track_offset`, `discid_get_track_length`, `discid_get_error_msg`,
`discid_get_default_device`, `discid_get_version_string`.

## Failure scenario

A caller writes `DiscReader.Read("/dev/sr1", DiscIdFeatures.Isrc)`. libdiscid
dutifully performs the Q-subchannel pass across the **entire disc** -- turning a
roughly one-second TOC read into a multi-minute one, exactly the cost
`DiscIdFeatures.cs:7-13`'s own remarks warn about -- and the returned `Disc` is
byte-for-byte identical to the `None` result. The flag is a pure, silent time
sink.

Nothing in-tree passes either flag today (the only call sites are
`DiscReader.Read(device)` at `DiscInfoCommand.cs:31` and
`MakeReleaseInfoCommand.cs:75`), so this is a live **trap** rather than a live
bug.

## Context

whatinator does obtain ISRC and the disc catalog number today -- from `cdrdao`,
via `Whatinator.Core.Toc` (`DiscTocTrack.Isrc`, `DiscToc.CatalogNumber`) -- not
from libdiscid. So there is currently no functional need for these flags. See
the separate backlog item on ISRC/UPC being captured but discarded.

## Acceptance Criteria

Pick one and do it fully -- do not leave the flags accepted-but-inert.

**Option A (remove):**
- [ ] `Mcn` and `Isrc` deleted from `DiscIdFeatures`; the enum reduced to `None`,
      or the `features` parameter removed from `DiscReader.Read` entirely.
- [ ] Doc comment records that ISRC/MCN come from cdrdao instead, and why.

**Option B (implement):**
- [ ] `discid_get_mcn` and `discid_get_track_isrc` bound in `NativeMethods.cs`,
      following the project's interop conventions (`IntPtr` return decoded with
      `Marshal.PtrToStringUTF8` inside the handle's lifetime).
- [ ] `Mcn` surfaced on `Disc`, `Isrc` on `Track`, populated only when the
      corresponding flag was requested.
- [ ] Doc comments state that the fields are `null` unless the flag was passed.
- [ ] New test asserting the fields are `null` for a `None` read.
- [ ] Manual verification against a real disc known to carry ISRCs, cross-checked
      against the values `cdrdao read-toc` reports for the same disc.
